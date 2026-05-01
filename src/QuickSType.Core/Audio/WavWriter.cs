using System.Buffers.Binary;

namespace QuickSType.Core.Audio;

public static class WavWriter
{
    public static byte[] WritePcm16(ReadOnlySpan<float> samples, int sampleRate = 16000, int channels = 1)
    {
        const int bitsPerSample = 16;
        var bytesPerSample = bitsPerSample / 8;
        var byteRate = sampleRate * channels * bytesPerSample;
        var blockAlign = channels * bytesPerSample;
        var dataSize = samples.Length * bytesPerSample;
        var totalSize = 44 + dataSize;

        var buffer = new byte[totalSize];
        var span = buffer.AsSpan();

        WriteAscii(span[..4], "RIFF");
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(4, 4), totalSize - 8);
        WriteAscii(span.Slice(8, 4), "WAVE");

        WriteAscii(span.Slice(12, 4), "fmt ");
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(16, 4), 16);
        BinaryPrimitives.WriteInt16LittleEndian(span.Slice(20, 2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(span.Slice(22, 2), (short)channels);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(24, 4), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(28, 4), byteRate);
        BinaryPrimitives.WriteInt16LittleEndian(span.Slice(32, 2), (short)blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(span.Slice(34, 2), (short)bitsPerSample);

        WriteAscii(span.Slice(36, 4), "data");
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(40, 4), dataSize);

        var data = span[44..];
        for (int i = 0; i < samples.Length; i++)
        {
            var s = Math.Clamp(samples[i], -1f, 1f);
            var pcm = (short)(s * short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(data.Slice(i * 2, 2), pcm);
        }

        return buffer;
    }

    public static MemoryStream WritePcm16Stream(ReadOnlySpan<float> samples, int sampleRate = 16000, int channels = 1)
    {
        var bytes = WritePcm16(samples, sampleRate, channels);
        return new MemoryStream(bytes, writable: false);
    }

    private static void WriteAscii(Span<byte> dest, string text)
    {
        for (int i = 0; i < text.Length && i < dest.Length; i++)
            dest[i] = (byte)text[i];
    }
}
