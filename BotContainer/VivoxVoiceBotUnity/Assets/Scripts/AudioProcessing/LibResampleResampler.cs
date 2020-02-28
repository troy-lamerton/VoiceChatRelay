using LibResample.Sharp;

/// <summary>
/// Resample audio data to another sample rate. Doesn't check the input bytes were used or the length of the output. 
/// </summary>
public class LibResampleResampler {
    private ReSampler resampler;
    private double factor;

    
    private readonly float[] inputBuffer; 
    private readonly float[] outputBuffer;
    
    public LibResampleResampler(uint input, uint output, int inputLength) {
        factor = output / (double) input;
        resampler = new ReSampler(false, factor, factor);
        inputBuffer = new float[inputLength];
        outputBuffer = new float[(int) (inputLength * factor)];
        Log.d($"created resampler for {input}->{output}, {inputBuffer.Length} -> {outputBuffer.Length} samples (factor {factor})");
    }

    public int ReadyChunksCount => 0;

    /// <summary>
    /// Use this method or the other one to add samples
    /// </summary>
    /// <param name="samples"></param>
    /// <param name="numSamples"></param>
    /// <param name="outBytes">fill me pls</param>
    /// <returns>True when valid result, else false</returns>
    public unsafe bool ProcessSamples(short* samples, int numSamples, byte[] outBytes) {
        var inputShorts = BitHelpers.ShortPointersToArray(samples, numSamples * 2);
        Accord.Audio.SampleConverter.Convert(inputShorts, inputBuffer);
        
        var res = resampler.Process(factor, inputBuffer, 0, inputBuffer.Length, false, outputBuffer, 0, outputBuffer.Length);

        Log.Assert(res.InputSamplesConsumed == inputBuffer.Length, $"Didnt consume all input samples! only {res.InputSamplesConsumed} / {inputBuffer.Length}");
        Log.Assert(res.OutputSamplesgenerated == outputBuffer.Length, $"Didnt create enough output samples! only {res.OutputSamplesgenerated} / {outputBuffer.Length}");
        
        BitHelpers.ToBytesFlipped(outputBuffer, outBytes);
        return res.OutputSamplesgenerated == outputBuffer.Length;
    }

    // resamples the bytes and writes them to the shorts
    public unsafe int ProcessBytesAndWrite(byte[] discordSampleBytes, short* outputSamples) {
        Accord.Audio.SampleConverter.Convert(discordSampleBytes, inputBuffer);
        var res = resampler.Process(factor, inputBuffer, 0, inputBuffer.Length, false, outputBuffer, 0, outputBuffer.Length);
        
        BitHelpers.WriteShorts(outputSamples, outputBuffer);
        
        return res.OutputSamplesgenerated;
    }
}
