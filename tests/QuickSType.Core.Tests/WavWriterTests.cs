using System.Buffers.Binary;
using QuickSType.Core.Audio;

namespace QuickSType.Core.Tests;

public class WavWriterTests
{
    [Fact]
    public void Writes_valid_riff_header()
    {
        var samples = new float[] { 0.0f, 0.5f, -0.5f, 1.0f, -1.0f };
        var wav = WavWriter.WritePcm16(samples, 16000);

        System.Text.Encoding.ASCII.GetString(wav, 0, 4).ShouldBe("RIFF");
        System.Text.Encoding.ASCII.GetString(wav, 8, 4).ShouldBe("WAVE");
        System.Text.Encoding.ASCII.GetString(wav, 12, 4).ShouldBe("fmt ");
        System.Text.Encoding.ASCII.GetString(wav, 36, 4).ShouldBe("data");

        BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(20, 2)).ShouldBe((short)1);
        BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(22, 2)).ShouldBe((short)1);
        BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(24, 4)).ShouldBe(16000);
        BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(34, 2)).ShouldBe((short)16);
        BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(40, 4)).ShouldBe(samples.Length * 2);
    }

    [Fact]
    public void Round_trips_pcm16_samples_within_quantization_error()
    {
        var samples = new float[1000];
        var rng = new Random(42);
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (float)(rng.NextDouble() * 2 - 1);

        var wav = WavWriter.WritePcm16(samples, 16000);
        var data = wav.AsSpan(44);

        for (int i = 0; i < samples.Length; i++)
        {
            var pcm = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(i * 2, 2));
            var roundTrip = pcm / (float)short.MaxValue;
            Math.Abs(roundTrip - samples[i]).ShouldBeLessThan(1e-3f);
        }
    }

    [Fact]
    public void Clamps_out_of_range_samples()
    {
        var samples = new float[] { 2.0f, -2.0f, 0.0f };
        var wav = WavWriter.WritePcm16(samples, 16000);
        var data = wav.AsSpan(44);

        BinaryPrimitives.ReadInt16LittleEndian(data.Slice(0, 2)).ShouldBe(short.MaxValue);
        BinaryPrimitives.ReadInt16LittleEndian(data.Slice(2, 2)).ShouldBe((short)-short.MaxValue);
        BinaryPrimitives.ReadInt16LittleEndian(data.Slice(4, 2)).ShouldBe((short)0);
    }
}
