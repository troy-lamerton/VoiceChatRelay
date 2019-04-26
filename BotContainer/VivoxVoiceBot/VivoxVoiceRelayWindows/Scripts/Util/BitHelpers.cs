using System;

public static class BitHelpers {
    public static byte[] ToBytes(short[] samples) {
        return ToBytes(samples, samples.Length);
    }

    public static byte[] ToBytes(short[] samples, int numSamples) {
        var bytes = new byte[numSamples * 2]; // 2 bytes make a short
        for (int i = 0; i < numSamples; i++) {
            ToBytes(samples[i], out bytes[i * 2], out bytes[i * 2 + 1]);
        }

        return bytes;
    }

    public static byte[] ToBytes(short number) {
        return new[] {
            (byte) (number >> 8),
            (byte) (number & 255)
        };
    }

    public static void ToBytes(short number, out byte byte1, out byte byte2) {
        // little endian - I confirmed this is code is legit
        byte1 = (byte) (number >> 8);
        byte2 = (byte) (number & 255);
    }

    public static unsafe void WriteBytes(byte[] buffer, short* samples, int numSamples, uint channels) {
        for (int i = 0; i < numSamples * channels; i++) {
            ToBytes(samples[i], out buffer[i * 2], out buffer[i * 2 + 1]);
        }
    }

    /// <param name="buffer"></param>
    /// <param name="samples"></param>
    /// <param name="numTotalSamples">The total number of samples to be written, probably same as samples.Length</param>
    public static void WriteBytes(byte[] buffer, short[] samples, int numTotalSamples) {
        for (int i = 0; i < numTotalSamples; i++) {
            ToBytes(samples[i], out buffer[i * 2], out buffer[i * 2 + 1]);
        }
    }

    public static void WriteBytes(byte[] buffer, short sample, int sampleOffset) {
        ToBytes(sample, out buffer[sampleOffset * 2], out buffer[sampleOffset * 2 + 1]);
    }

    // replace samples with the bytes in inputBuffer
    public static unsafe void WriteShorts(short* samples, byte[] inputBuffer, int inputOffset, int numSamples) {
        Log.Assert(inputBuffer.Length >= numSamples * 2,
            $"not enough input bytes ({inputBuffer.Length}) to fill {numSamples} 16bit samples!");
        for (int i = 0; i < numSamples; i++) {
            samples[i] = BitConverter.ToInt16(inputBuffer, inputOffset + i * 2);
        }
    }

    public static unsafe void WriteShorts(short* samples, short[] inputSamples, long outputOffset, uint numSamples) {
        Log.Assert(inputSamples.Length >= numSamples,
            $"not enough input bytes ({inputSamples.Length}) to fill {numSamples} 16bit samples!");
        for (int i = 0; i < numSamples; i++) {
            samples[outputOffset + i] = inputSamples[i];
        }
    }

    public static void LittleToBigEndian(byte[] buffer) {
        for (var i = 0; i < buffer.Length; i += 2) {
            var temp = buffer[i];
            buffer[i] = buffer[i + 1];
            buffer[i + 1] = temp;
        }
    }
}
