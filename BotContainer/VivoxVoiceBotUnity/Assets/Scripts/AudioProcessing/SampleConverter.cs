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

    private readonly LibResampleResampler resamplerVivoxToDiscord;
    private readonly LibResampleResampler resamplerDiscordToVivox;
    private static readonly CancellationTokenSource cans = new CancellationTokenSource();
    private readonly CancellationToken cancelResamplingToken = cans.Token;
    private byte[] discordInputBytes; // discord to vivox only 

    public SampleConverter() {
        // vivox speakers to discord
        resamplerVivoxToDiscord = new LibResampleResampler(VIVOX_SAMPLE_RATE, DISCORD_SAMPLE_RATE,
            MONO_32_KHZ_20_MS_BYTE_COUNT); // not / 2 because stereo

        // discord samples inject into vivox
        resamplerDiscordToVivox = new LibResampleResampler(DISCORD_SAMPLE_RATE, VIVOX_SAMPLE_RATE, MONO_48_KHZ_20_MS_BYTE_COUNT / 2);
    }

    private readonly byte[] forDiscordBytes = new byte[STEREO_48_KHZ_20_MS_BYTE_COUNT];

    private readonly ReaderWriterLockSlim forDiscordLock = new ReaderWriterLockSlim();
    private readonly ReaderWriterLockSlim resampleForVivoxLock = new ReaderWriterLockSlim();

    ~SampleConverter() {
        cans.Cancel();
    }

    private readonly Stopwatch stopwatch = new Stopwatch();
    private Stopwatch stopwatch2 = new Stopwatch();

    public unsafe void ResampleAndSendToDiscordBot(short* inputSamples, int inputSampleRate, int numInputSamples,
        UdpUser udpClient) {
        var success = Task.Run(() => {
            Log.v($"Sending forDiscordBytes");

            stopwatch.Restart();

            if (inputSampleRate == DISCORD_SAMPLE_RATE) {
                // no resampling needed
                Log.w( $"Not resampling vivox to discord (already correct sample rate)");
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
            var validConversion = resamplerVivoxToDiscord.ProcessSamples(inputSamples, numInputSamples, forDiscordBytes);
            forDiscordLock.ExitWriteLock();

            stopwatch.Stop();
            Log.Assert(stopwatch.ElapsedMilliseconds <= 12,
                $"took {stopwatch.ElapsedMilliseconds} ms to resample for discord!");

            if (validConversion) {
                udpClient.SendSync(forDiscordBytes);
            }
        }, cancelResamplingToken).Wait(19);
        Log.Assert(success, "timed out resampling audio from vivox ppls -> discord");
    }

    public void AddDiscordSample(byte[] data) {
        BitHelpers.LittleToBigEndian(data);
        discordInputBytes = data;
    }

    public int SamplesReadyForVivox => resamplerDiscordToVivox.ReadyChunksCount;

    /// <summary>mono in and mono is written</summary>
    public unsafe int WriteDiscordAudioToVivox(short* outputSamples, int outputSampleRate, int numOutputSamples) {
        if (outputSampleRate != VIVOX_SAMPLE_RATE) return 0;

        stopwatch2 = Stopwatch.StartNew();
        Log.Assert(outputSampleRate == VIVOX_SAMPLE_RATE, $"Unexpected vivox output rate of {outputSampleRate}");
        Log.Assert(numOutputSamples * 3 == MONO_48_KHZ_20_MS_BYTE_COUNT,
            $"{numOutputSamples} * 3 == {MONO_48_KHZ_20_MS_BYTE_COUNT}");


        resampleForVivoxLock.EnterWriteLock(); // writing to the output buffer in the resampler

        // convert them to vivox current sample rate
//        int converted = resamplerDiscordToVivox.ProcessBytesAndWrite(discordInputBytes, outputSamples);
        resampleForVivoxLock.ExitWriteLock();

        stopwatch2.Stop();

        Log.Assert(stopwatch2.ElapsedMilliseconds <= 10,
            $"took {stopwatch2.ElapsedMilliseconds} ms to resample for vivox mic!");
        return numOutputSamples;
//        return converted;
    }

    public const int DISCORD_SAMPLE_RATE = 48000;
    public const int VIVOX_SAMPLE_RATE = 32000; // most common sample rate that vivox uses

    public const int MONO_32_KHZ_20_MS_BYTE_COUNT = VIVOX_SAMPLE_RATE / 50 * 2;
    public const int MONO_48_KHZ_20_MS_BYTE_COUNT = DISCORD_SAMPLE_RATE / 50 * 2;
    public const int STEREO_48_KHZ_20_MS_BYTE_COUNT = MONO_48_KHZ_20_MS_BYTE_COUNT * 2; // discord

    public const int MAX_CHUNKS_WAITING = 10;
}

