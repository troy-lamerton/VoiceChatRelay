using System;
using System.Runtime.InteropServices;
using VivoxUnity;

public class AudioProcessor {
    private readonly UdpUser udpClient;
    private readonly SampleConverter sampleConverter;

    private bool _enabled = true; 
    public bool Enabled {
        set {
            Log.i($"AudioProcesser -> {(value ? "Enabled" : "Disabled")}");
            _enabled = value;
        }
        private get => _enabled;
    }

    private bool gotFirstReceived = false;
    private bool gotFirstMic = false;
    
    public AudioProcessor(UdpUser udpClient) {
        this.udpClient = udpClient;
        sampleConverter = new SampleConverter();
    }

    private unsafe delegate void MicrophoneAudioDelegate(
        IntPtr callbackHandle,
        char id1,
        char id2,
        short* pcmFrames,
        int frameCount,
        int frameRate,
        int channels
    );

    private unsafe delegate void ProcessedAudioDelegate(
        IntPtr callbackHandle,
        char id1,
        char id2,
        short* pcmFrames,
        int frameCount,
        int frameRate,
        int channels,
        int isSilence
    );


    public void AddSample(byte[] sample) {
        if (Enabled) sampleConverter.AddDiscordSample(sample);
    }

    private static ProcessedAudioDelegate audioReceivedDelegate;
    private static MicrophoneAudioDelegate activateMicrophoneDelegate;
    private static ProcessedAudioDelegate transformSentProcessedAudioDelegate;
    
    public unsafe Client InitVivoxClient() {
        var client = new Client();
        
        audioReceivedDelegate = ReceivedAudioCallback;
        activateMicrophoneDelegate = ActivateMicrophoneCallback;
        transformSentProcessedAudioDelegate = InjectDiscordAudioCallback;
        
        var receivedAudio =
            Marshal.GetFunctionPointerForDelegate(audioReceivedDelegate);
        var activateMicrophone = Marshal.GetFunctionPointerForDelegate(activateMicrophoneDelegate);
        var injectDiscordAudio = Marshal.GetFunctionPointerForDelegate(transformSentProcessedAudioDelegate);
        
        client.Initialize(receivedAudio, activateMicrophone, injectDiscordAudio);
        
        return client;
    }

    /// <summary>
    /// Inject sine wave to make vivox try to send the audio data 
    /// </summary>
    private static unsafe void ActivateMicrophoneCallback(IntPtr _, char id1, char id2, short* pcmFrames, int frameCount,
        int frameRateHz, int channels) {
        // sine wave
        double amplitude = 0.24 * short.MaxValue;
        double waveFrequency = 1000;
        for (int i = 0; i < frameCount; i++) {
            pcmFrames[i] = (short) (amplitude * Math.Sin((2 * Math.PI * i * waveFrequency) / frameRateHz));
        }
    }

    /// <summary>
    /// Called just before sending bot's microphone data to other vivox room members
    /// Replace audio to be sent to vivox players with the audio of people talking in discord or silence
    /// Input: Mono shorts @ 32KHz or 48KHz or (rarely) another sample rate
    /// Input: Sample from discord, mono @ 48KHz as byte[]
    /// Resampling: 48KHz byte[] -> 32KHz (or other) byte[] -> write these bytes into the short* memory
    /// Output: Replaces the mono shorts with resampled discord input
    /// </summary>
    private unsafe void InjectDiscordAudioCallback(IntPtr _, char id1, char id2, short* pcmFrames, int numFrames,
        int sampleRate, int channels, int isSilence) {
        if (!gotFirstMic) {
            gotFirstMic = true;
            Log.i($"Wow we got a mic inject cb with {numFrames} frames!");
        }
        Log.Assert(channels == 1, "channels == 1");
        if (Enabled && sampleConverter.SamplesReadyForVivox >= 4) {
            // if samples are ready and succeed in writing the frames, we good
            if (sampleConverter.WriteDiscordAudioToVivox(pcmFrames, sampleRate, numFrames) >= numFrames) {
                return;
            }
        }

        // audio from discord is not ready or was not written
        // replace the sine wave (created by ActivateMicrophoneCallback) with silence
        for (int i = 0; i < numFrames; i++) {
            pcmFrames[i] = 0;
        }
    }

    /// <summary>
    /// Called when audio is received from players in the vivox room
    /// Sends this audio to the discord bot.
    /// Called every 20ms in testing. This can be verified by doing 1000 / frameRateHz / frameCount.
    /// May be called even when no one is speaking.
    /// </summary>
    /// <param name="_"></param>
    /// <param name="id1"></param>
    /// <param name="id2"></param>
    /// <param name="pcmFrames"></param>
    /// <param name="numFrames">640 or 960 during testing</param>
    /// <param name="sampleRate">32000 or 48000</param>
    /// <param name="channels">2</param>
    /// <param name="isSilence">0 if audio contains speaking data, 1 if only silence</param>
    ///
    private unsafe void ReceivedAudioCallback(IntPtr _, char id1, char id2, short* pcmFrames, int numFrames,
        int sampleRate, int channels, int isSilence) {
        if (!gotFirstReceived) {
            gotFirstReceived = true;
            Log.i($"Wow we got an received with {numFrames} frames!");
        }
        if (!Enabled) return;
        
        // ignore silent samples
//        if (isSilence == 1) return;
        
#if DEBUG
        if (isSilence == 0 && !(channels == 2 && (numFrames == 640 || numFrames == 960))) {
            Log.w($"unexpected audio format! {channels} channels @ {sampleRate} Hz ({numFrames} frames)");
            return;
        }
#endif
        
        sampleConverter.ResampleAndSendToDiscordBot(pcmFrames, sampleRate, numFrames, udpClient);
    }
}
