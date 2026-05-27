namespace QuickSType.Core.Audio;

/// <summary>
/// Pure helper that computes the normalised RMS (0..1) of a buffer of float audio samples.
/// Designed to be called from a real-time audio callback: no allocations, no logging,
/// no exceptions for sane inputs. NaN/Infinity in the input are treated as 0.
/// </summary>
internal static class AudioLevelCalculator
{
    /// <summary>
    /// Compute sqrt(mean(x^2)) clamped into [0,1]. Returns 0 for empty/all-zero/NaN spans.
    /// </summary>
    internal static float ComputeNormalisedRms(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty) return 0f;

        double sumSq = 0d;
        int valid = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float s = samples[i];
            if (float.IsNaN(s) || float.IsInfinity(s)) continue;
            sumSq += (double)s * s;
            valid++;
        }

        if (valid == 0) return 0f;

        double mean = sumSq / valid;
        double rms = Math.Sqrt(mean);

        if (double.IsNaN(rms) || double.IsInfinity(rms)) return 0f;
        if (rms <= 0d) return 0f;
        if (rms >= 1d) return 1f;
        return (float)rms;
    }
}
