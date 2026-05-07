namespace QuickSType.Core.Transcribe;

public sealed class ModelIntegrityException : Exception
{
    public string ModelId { get; }
    public string ExpectedSha256 { get; }
    public string ActualSha256 { get; }

    public ModelIntegrityException(string modelId, string expected, string actual)
        : base($"SHA-256 mismatch for {modelId}: expected {expected}, actual {actual}")
    {
        ModelId = modelId;
        ExpectedSha256 = expected;
        ActualSha256 = actual;
    }
}
