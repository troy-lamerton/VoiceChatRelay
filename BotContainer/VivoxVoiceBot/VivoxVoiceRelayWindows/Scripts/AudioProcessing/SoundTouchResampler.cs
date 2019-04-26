using System;
using soundtouch;

/// <summary>
/// Resample audio data to another sample rate. Doesn't check the input bytes were used or the length of the output. 
/// </summary>
public class SoundTouchResampler : IDisposable {
    private readonly SoundTouch soundTouch = new SoundTouch();
    private readonly short[] inputBuffer;
    private readonly short[] resampledBuffer;
    private readonly uint channels;

    public SoundTouchResampler(uint input, uint output, uint channels) {
        this.channels = channels;
        
        uint inputLength = input / 50 * channels;
        uint outputLength = output / 50 * channels;
        inputBuffer = new short[inputLength];
        resampledBuffer = new short[outputLength];
        Log.w($"Created resampler buffers: {inputLength} -> {outputLength}");
        
        soundTouch[SoundTouch.Setting.UseQuickSeek] = 1;
        soundTouch.Channels = channels;
        soundTouch.SampleRate = input;
        
        float conversionFraction = output / (float) input;
        soundTouch.Rate = conversionFraction;
        soundTouch.Tempo = 1 / conversionFraction;
        soundTouch.Pitch = 1 / conversionFraction;
        
        Log.d($"SoundTouch latency {input / 1000}k -> {output / 1000}k: {soundTouch[SoundTouch.Setting.InitialLatency]}, input samples needed: {soundTouch[SoundTouch.Setting.NominalInputSequence]}");
    }

    public int ReadyChunksCount => (int) (soundTouch.AvailableSampleCount / resampledBuffer.Length);

    public unsafe void AddSamples(short* samples, int numSamples) {
        DropIfNeeded();
        
        for (int i = 0; i < numSamples * channels; i++) {
            inputBuffer[i] = samples[i];
        }

        soundTouch.PutSamplesI16(inputBuffer, (uint) numSamples);
    }

    public void AddSamples(byte[] discordSampleBytes) {
        DropIfNeeded();
        
        for (int i = 0; i < discordSampleBytes.Length; i += 2) {
            inputBuffer[i / 2] = BitConverter.ToInt16(discordSampleBytes, i);
        }

        uint discordNumSamples = (uint) (discordSampleBytes.Length / 2 / channels);

        soundTouch.PutSamplesI16(inputBuffer, discordNumSamples);
    }

    private void DropIfNeeded(bool log = true) {
        if (ReadyChunksCount < SampleConverter.MAX_CHUNKS_WAITING) return;
        
        // latency getting too high, drop some samples
        if (log) Log.w($"Dropping 2 x {resampledBuffer.Length} samples bcus {ReadyChunksCount} 20ms chunks are in the buffer");
        soundTouch.ReceiveSamplesI16(resampledBuffer, (uint) (resampledBuffer.Length / channels));
        soundTouch.ReceiveSamplesI16(resampledBuffer, (uint) (resampledBuffer.Length / channels));
    }

    /// <param name="outputSampleRate">Sample rate desired by vivox</param>
    /// <param name="outputNumFrames">Number of audio frames</param>
    /// <param name="outputSamples">Buffer to be filled with the resampled audio frames</param>
    public unsafe uint GetOutputSamples(short* outputSamples, int outputSampleRate, uint outputNumFrames) {
        uint numReceivedSamples = soundTouch.ReceiveSamplesI16(resampledBuffer, outputNumFrames);
        BitHelpers.WriteShorts(outputSamples, resampledBuffer, 0, numReceivedSamples);

        Log.Assert(numReceivedSamples == outputNumFrames,
            $"unexpected resample to vx result: {SampleConverter.MONO_48_KHZ_20_MS_BYTE_COUNT / 2} @ {SampleConverter.DISCORD_SAMPLE_RATE:D5}" +
            $" -> {numReceivedSamples:D5} @ {outputSampleRate:D5} (expected {outputNumFrames:D5} frames)");

        return numReceivedSamples;
    }

    /// <param name="outputBytes">Buffer to be filled</param>
    /// <param name="numBytes">Number of bytes the output chunk should be</param>
    public void GetOutputBytes(byte[] outputBytes, int numBytes) {
        Log.Assert(numBytes / 2 == resampledBuffer.Length, $"failed: {numBytes} / 2 == {resampledBuffer.Length}");

        uint numReceivedSamples = soundTouch.ReceiveSamplesI16(resampledBuffer, (uint) (numBytes / 2 / channels));
        Log.Assert(numReceivedSamples * channels == numBytes / 2, $"unexpected resample to discord result: got {numReceivedSamples} x {channels} output samples");
        Log.Assert(numReceivedSamples * channels == SampleConverter.STEREO_48_KHZ_20_MS_BYTE_COUNT / 2, $"incorrect num bytes being sent, {numReceivedSamples * 4} != {SampleConverter.STEREO_48_KHZ_20_MS_BYTE_COUNT}");
        BitHelpers.WriteBytes(outputBytes, resampledBuffer, resampledBuffer.Length);
    }

    public void Dispose() {
        soundTouch?.Clear();
    }
}
