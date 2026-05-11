namespace QuickSType.Core.Platform;

/// <summary>
/// Represents a keyboard input layout detected by <see cref="IKeyboardLayoutService"/>.
/// </summary>
/// <param name="DisplayName">OS-provided layout name e.g. "English (US)" on Mac, "ENG" on Windows.</param>
/// <param name="Code">BCP-47 locale code e.g. "en-US", "he-IL". Maps to closest Whisper language code for Phase 4 language hint (STREAM-06).</param>
public readonly record struct InputLayout(string DisplayName, string Code)
{
    public override string ToString() => DisplayName;
}
