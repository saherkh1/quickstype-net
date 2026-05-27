using QuickSType.Core.Audio;
using Shouldly;
using Xunit;

namespace QuickSType.Core.Tests.Audio;

public class AudioLevelCalculatorTests
{
    [Fact]
    public void Empty_span_returns_zero()
    {
        AudioLevelCalculator.ComputeNormalisedRms(ReadOnlySpan<float>.Empty).ShouldBe(0f);
    }

    [Fact]
    public void All_zero_returns_zero()
    {
        var buf = new float[1024];
        AudioLevelCalculator.ComputeNormalisedRms(buf).ShouldBe(0f);
    }

    [Fact]
    public void All_plus_one_returns_one()
    {
        var buf = new float[1024];
        Array.Fill(buf, 1f);
        AudioLevelCalculator.ComputeNormalisedRms(buf).ShouldBe(1f, tolerance: 1e-4f);
    }

    [Fact]
    public void Sine_wave_is_monotonic_in_amplitude()
    {
        // Louder sine wave must produce a strictly larger RMS than a quieter one.
        var quiet = MakeSine(amplitude: 0.25f, length: 1024, periods: 8);
        var loud = MakeSine(amplitude: 0.75f, length: 1024, periods: 8);

        var quietRms = AudioLevelCalculator.ComputeNormalisedRms(quiet);
        var loudRms = AudioLevelCalculator.ComputeNormalisedRms(loud);

        loudRms.ShouldBeGreaterThan(quietRms);

        // Sanity: amplitude 0.5 sine -> RMS ~ 0.5/sqrt(2) = ~0.3536
        var half = MakeSine(0.5f, 1024, 8);
        var halfRms = AudioLevelCalculator.ComputeNormalisedRms(half);
        halfRms.ShouldBe(0.5f / MathF.Sqrt(2f), tolerance: 0.01f);
    }

    [Fact]
    public void NaN_and_infinity_are_treated_as_zero()
    {
        var buf = new float[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity };
        AudioLevelCalculator.ComputeNormalisedRms(buf).ShouldBe(0f);
    }

    [Fact]
    public void NaN_mixed_with_real_samples_ignores_NaN_entries()
    {
        var buf = new float[] { float.NaN, 1f, float.NaN, 1f };
        // Two valid samples both equal to 1.0 -> RMS = 1.0
        AudioLevelCalculator.ComputeNormalisedRms(buf).ShouldBe(1f, tolerance: 1e-4f);
    }

    [Fact]
    public void Result_always_in_zero_one_range()
    {
        var inputs = new[]
        {
            new float[] { 0f },
            new float[] { -1f, 1f, -1f, 1f },
            new float[] { 5f, -5f, 5f, -5f }, // out-of-range native audio -> clamped to 1
            new float[] { 0.0001f, -0.0001f },
        };

        foreach (var buf in inputs)
        {
            var rms = AudioLevelCalculator.ComputeNormalisedRms(buf);
            rms.ShouldBeGreaterThanOrEqualTo(0f);
            rms.ShouldBeLessThanOrEqualTo(1f);
        }
    }

    private static float[] MakeSine(float amplitude, int length, int periods)
    {
        var buf = new float[length];
        for (int i = 0; i < length; i++)
        {
            double t = (double)i / length;
            buf[i] = amplitude * MathF.Sin((float)(2 * Math.PI * periods * t));
        }
        return buf;
    }
}
