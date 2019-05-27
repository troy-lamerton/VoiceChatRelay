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

    public static void ToBytesFlipped(short[] samples, byte[] outBuffer) {
        for (int i = 0; i < samples.Length; i++) {
            ToBytes(samples[i], out outBuffer[i * 2 + 1], out outBuffer[i * 2]);
        }
    }

    public static void ToBytesFlipped(float[] from, byte[] to) {
        for (int i = 0; i < from.Length; ++i) {
            short sample = (short) ((double) from[i] * (double) short.MaxValue);
            ToBytes(sample, out to[i * 2 + 1], out to[i * 2]);
        }
    }

    public static byte[] ToBytes(short number) {
        return new[] {
            (byte) (number >> 8),
            (byte) (number & 255)
        };
    }

    public static void ToBytes(short number, out byte byte1, out byte byte2) {
        // little endian - I confirmed this code is legit
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

    public static unsafe void WriteShorts(short* samples, float[] inputSamples) {
        for (int i = 0; i < inputSamples.Length; i++) {
            samples[i] = (short) ((double) inputSamples[i] * (double) short.MaxValue);
        }
    }

    public static unsafe short[] ShortPointersToArray(short* samples, int numSamples) {
        var arr = new short[numSamples];
        for (int i = 0; i < numSamples; i++) {
            arr[i] = samples[i];
        }
        return arr;
    }

    private static byte[] twoBytes = new byte[2];
    
    public static unsafe short[] ShortPointersToLEArray(short* samples, int numSamples) {
        var arr = new short[numSamples];
        for (int i = 0; i < numSamples; i++) {
            ToBytes(samples[i], out twoBytes[1], out twoBytes[0]);
            arr[i] = BitConverter.ToInt16(twoBytes, 0);
        }

        return arr;
    }

    public static short LittleToBigEndian(short bigEndianSample) {
        var bytes = ToBytes(bigEndianSample);
        return BitConverter.ToInt16(new[] {bytes[1], bytes[0]}, 0);
    }

    public static void LittleToBigEndian(byte[] buffer) {
        for (var i = 0; i < buffer.Length; i += 2) {
            var temp = buffer[i];
            buffer[i] = buffer[i + 1];
            buffer[i + 1] = temp;
        }
    }
}
