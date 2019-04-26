using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Resample 20ms audio samples to another sample rate
/// All array parameters expect 20ms of audio data
/// All returned byte[] are 20ms of audio data 
/// </summary>
public class SampleConverter {

    private readonly SoundTouchResampler resamplerVivoxToDiscord;
    private readonly SoundTouchResampler resamplerDiscordToVivox;

    public SampleConverter() {
        // vivox speakers to discord
        resamplerVivoxToDiscord = new SoundTouchResampler(VIVOX_SAMPLE_RATE, DISCORD_SAMPLE_RATE, 2);

        // discord samples inject into vivox
        resamplerDiscordToVivox = new SoundTouchResampler(DISCORD_SAMPLE_RATE, VIVOX_SAMPLE_RATE, 1);
    }

    private readonly byte[] forDiscordBytes = new byte[STEREO_48_KHZ_20_MS_BYTE_COUNT];

    private readonly ReaderWriterLockSlim forDiscordLock = new ReaderWriterLockSlim();
    private readonly ReaderWriterLockSlim resampleForVivoxLock = new ReaderWriterLockSlim();

    ~SampleConverter() {
        resamplerVivoxToDiscord.Dispose();
        resamplerDiscordToVivox.Dispose();
    }

    private readonly Stopwatch stopwatch = new Stopwatch();
    private Stopwatch stopwatch2 = new Stopwatch();

    public unsafe void ResampleAndSendToDiscordBot(short* inputSamples, int inputSampleRate, int numInputSamples,
        UdpUser udpClient) {
        Log.i($"Sending forDiscordBytes to dbot udp server");
        Task.Run(() => {
            Log.i($"task inner - Sending forDiscordBytes ");

            stopwatch.Restart();

            if (inputSampleRate == DISCORD_SAMPLE_RATE) {
                // no resampling needed
                forDiscordLock.EnterWriteLock();
                BitHelpers.WriteBytes(forDiscordBytes, inputSamples, numInputSamples, 2);
                forDiscordLock.ExitWriteLock();

                forDiscordLock.EnterReadLock();
                udpClient.SendSync(forDiscordBytes);
                forDiscordLock.ExitReadLock();

                return;
            }

            if (inputSampleRate != VIVOX_SAMPLE_RATE) {
                Log.e( $"Vivox input sample rate {inputSampleRate} is unexpected! Expected Vivox rate to be 32k or 48k.");
                return;
            }

            forDiscordLock.EnterWriteLock();
            resamplerVivoxToDiscord.AddSamples(inputSamples, numInputSamples);
            forDiscordLock.ExitWriteLock();

            if (SamplesReadyForDiscord >= 4) { 
                GetSamplesForDiscord(forDiscordBytes);
            } else {
                Log.w($"Not enough samples ready for discord, only {SamplesReadyForDiscord}");
                return;
            }

            stopwatch.Stop();
            Log.Assert(stopwatch.ElapsedMilliseconds <= 10,
                $"took {stopwatch.ElapsedMilliseconds} ms to resample for discord!");
            
            udpClient.SendAsync(forDiscordBytes).Wait();
        }).Wait(20);
    }

    /// <summary>
    /// Output: byte[] is filled with 48Khz stereo
    /// </summary>
    private void GetSamplesForDiscord(byte[] buffer) {
        forDiscordLock.EnterWriteLock();
        resamplerVivoxToDiscord.GetOutputBytes(buffer, buffer.Length);
        forDiscordLock.ExitWriteLock();
    }
    
    public void AddDiscordSample(byte[] data) {
        resamplerDiscordToVivox.AddSamples(data);
    }

    public int SamplesReadyForVivox => resamplerDiscordToVivox.ReadyChunksCount;
    public int SamplesReadyForDiscord => resamplerVivoxToDiscord.ReadyChunksCount;

    /// <summary>mono in and mono is written</summary>
    public unsafe uint WriteDiscordAudioToVivox(short* outputSamples, int outputSampleRate, int numOutputSamples) {
        if (outputSampleRate != VIVOX_SAMPLE_RATE) return 0;

        stopwatch2 = Stopwatch.StartNew();
        Log.Assert(outputSampleRate == VIVOX_SAMPLE_RATE, $"Unexpected vivox output rate of {outputSampleRate}");
        Log.Assert(numOutputSamples * 3 == MONO_48_KHZ_20_MS_BYTE_COUNT,
            $"{numOutputSamples} * 3 == {MONO_48_KHZ_20_MS_BYTE_COUNT}");


        resampleForVivoxLock.EnterWriteLock(); // writing to the output buffer in the resampler

        // convert them to vivox current sample rate
        uint converted = resamplerDiscordToVivox.GetOutputSamples(outputSamples, outputSampleRate, (uint) numOutputSamples);
        resampleForVivoxLock.ExitWriteLock();

        stopwatch2.Stop();

        Log.Assert(stopwatch2.ElapsedMilliseconds <= 10,
            $"took {stopwatch2.ElapsedMilliseconds} ms to resample for vivox mic!");
        return converted;
    }

    public const int DISCORD_SAMPLE_RATE = 48000;
    public const int VIVOX_SAMPLE_RATE = 32000; // most common sample rate that vivox uses

    public const int MONO_32_KHZ_20_MS_BYTE_COUNT = VIVOX_SAMPLE_RATE / 50 * 2;
    public const int MONO_48_KHZ_20_MS_BYTE_COUNT = DISCORD_SAMPLE_RATE / 50 * 2;
    public const int STEREO_48_KHZ_20_MS_BYTE_COUNT = MONO_48_KHZ_20_MS_BYTE_COUNT * 2; // discord

    public const int MAX_CHUNKS_WAITING = 10;
}

