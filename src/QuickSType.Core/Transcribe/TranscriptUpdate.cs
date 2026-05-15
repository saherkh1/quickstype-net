namespace QuickSType.Core.Transcribe;

public readonly record struct TranscriptUpdate(int RetractChars, string AppendText);
