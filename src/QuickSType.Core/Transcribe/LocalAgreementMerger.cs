namespace QuickSType.Core.Transcribe;

public sealed class LocalAgreementMerger
{
    private string _committed = string.Empty;

    public TranscriptUpdate Merge(string newHypothesis)
    {
        if (newHypothesis is null) throw new ArgumentNullException(nameof(newHypothesis));

        int lcp = LongestCommonPrefix(_committed, newHypothesis);

        // Snap LCP back to a word boundary to avoid mid-word splits.
        // Only snap when both strings continue beyond lcp with non-whitespace content,
        // meaning the split point is inside a word in both.
        if (lcp > 0
            && lcp < _committed.Length
            && lcp < newHypothesis.Length
            && !char.IsWhiteSpace(_committed[lcp - 1])
            && !char.IsWhiteSpace(newHypothesis[lcp - 1]))
        {
            while (lcp > 0
                   && !char.IsWhiteSpace(_committed[lcp - 1])
                   && !char.IsWhiteSpace(newHypothesis[lcp - 1]))
            {
                lcp--;
            }
        }

        // Additionally, if committed ends mid-word and newHyp extends it further
        // (committed is a strict prefix of newHyp), snap to the last word boundary
        // so partial words in committed are retracted and the full word is re-emitted.
        if (lcp == _committed.Length
            && lcp < newHypothesis.Length
            && lcp > 0
            && !char.IsWhiteSpace(_committed[lcp - 1]))
        {
            while (lcp > 0 && !char.IsWhiteSpace(_committed[lcp - 1]))
            {
                lcp--;
            }
        }

        int retract = _committed.Length - lcp;
        string append = newHypothesis[lcp..];
        _committed = newHypothesis;
        return new TranscriptUpdate(retract, append);
    }

    public void Reset() => _committed = string.Empty;

    private static int LongestCommonPrefix(string a, string b)
    {
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
        {
            if (a[i] != b[i]) return i;
        }
        return n;
    }
}
