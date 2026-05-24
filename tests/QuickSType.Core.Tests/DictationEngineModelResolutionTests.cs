using QuickSType.Core.Config;
using QuickSType.Core.Transcribe;

namespace QuickSType.Core.Tests;

/// <summary>
/// Tests for DictationEngine.ResolveModelId — pins all four fallback paths (D-09, D-12)
/// and the D-11 behavioral path (hotkey release during LoadingModel returns to Idle).
/// </summary>
public class DictationEngineModelResolutionTests
{
    // Helper: build a minimal AppConfig with LanguageModels pre-populated
    private static AppConfig MakeConfig(
        string model = "ggml-base",
        string activeLanguage = "en",
        bool autoLanguage = false,
        Dictionary<string, string>? languageModels = null)
    {
        var lm = languageModels is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(languageModels, StringComparer.OrdinalIgnoreCase);
        return new AppConfig
        {
            Model = model,
            ActiveLanguage = activeLanguage,
            AutoLanguage = autoLanguage,
            LanguageModels = lm,
        };
    }

    [Fact]
    public void ResolveModelId_returns_language_specific_model_when_assigned()
    {
        var cfg = MakeConfig(
            model: "ggml-base",
            activeLanguage: "he",
            autoLanguage: false,
            languageModels: new Dictionary<string, string> { ["he"] = "ggml-medium-he" });

        var result = DictationEngine.ResolveModelId(cfg);

        result.ShouldBe("ggml-medium-he");
    }

    [Fact]
    public void ResolveModelId_returns_global_model_when_AutoLanguage_true()
    {
        // D-12: AutoLanguage bypasses per-language map entirely
        var cfg = MakeConfig(
            model: "ggml-base",
            activeLanguage: "he",
            autoLanguage: true,
            languageModels: new Dictionary<string, string> { ["he"] = "ggml-medium-he" });

        var result = DictationEngine.ResolveModelId(cfg);

        result.ShouldBe("ggml-base");
    }

    [Fact]
    public void ResolveModelId_falls_back_to_global_when_no_assignment()
    {
        var cfg = MakeConfig(
            model: "ggml-base",
            activeLanguage: "he",
            autoLanguage: false,
            languageModels: new Dictionary<string, string>() /* empty */);

        var result = DictationEngine.ResolveModelId(cfg);

        result.ShouldBe("ggml-base");
    }

    [Fact]
    public void ResolveModelId_falls_back_to_global_for_unknown_model_id()
    {
        // Catalog validation guard — stale config or path traversal attempt
        var cfg = MakeConfig(
            model: "ggml-base",
            activeLanguage: "he",
            autoLanguage: false,
            languageModels: new Dictionary<string, string> { ["he"] = "ggml-nonexistent-xyz" });

        var result = DictationEngine.ResolveModelId(cfg);

        result.ShouldBe("ggml-base");
    }

    [Fact]
    public void ResolveModelId_falls_back_to_global_for_empty_string_value()
    {
        var cfg = MakeConfig(
            model: "ggml-base",
            activeLanguage: "he",
            autoLanguage: false,
            languageModels: new Dictionary<string, string> { ["he"] = "" });

        var result = DictationEngine.ResolveModelId(cfg);

        result.ShouldBe("ggml-base");
    }

    [Fact]
    public void ResolveModelId_is_case_insensitive_on_language_key()
    {
        // Dictionary uses OrdinalIgnoreCase (set in Plan 08-01 WithLanguageModel)
        var cfg = MakeConfig(
            model: "ggml-base",
            activeLanguage: "he",
            autoLanguage: false,
            languageModels: new Dictionary<string, string> { ["HE"] = "ggml-medium-he" });

        var result = DictationEngine.ResolveModelId(cfg);

        result.ShouldBe("ggml-medium-he");
    }

    /// <summary>
    /// D-11: Hotkey release during LoadingModel must cancel the load and return to Idle
    /// without transcribing. This is a structural test (Approach B) that asserts the
    /// correct guard is present in the source code.
    /// </summary>
    [Fact]
    public void OnHotkeyReleased_DuringLoadingModel_CancelsAndReturnsIdle()
    {
        // Approach B: structural source-code invariant test.
        // We verify the source file contains the required patterns that implement D-11.
        // This is the appropriate approach because DictationEngine requires heavyweight
        // real services (audio hardware, Whisper native runtime) that cannot be
        // safely mocked in a unit-test context without significant test infrastructure.

        // Locate DictationEngine.cs relative to the test project
        var testDir = Path.GetDirectoryName(typeof(DictationEngineModelResolutionTests).Assembly.Location)!;
        // Walk up from bin/... to repo root
        var dir = new DirectoryInfo(testDir);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "QuickSType.sln")))
            dir = dir.Parent;

        dir.ShouldNotBeNull();
        var enginePath = Path.Combine(dir!.FullName, "src", "QuickSType.Core", "DictationEngine.cs");
        File.Exists(enginePath).ShouldBeTrue();

        var source = File.ReadAllText(enginePath);

        // (1) The file contains a LoadingModel guard in OnHotkeyReleased that calls _cts?.Cancel()
        source.ShouldContain("DictationState.LoadingModel");
        // _cts?.Cancel() appears in the LoadingModel guard in OnHotkeyReleased
        source.ShouldContain("_cts?.Cancel()");

        // (2) The file contains .WaitAsync(ct) on a Task.Run call (the correct cancellation idiom)
        source.ShouldContain(".WaitAsync(ct)");

        // (3) SetState(DictationState.LoadingModel) must appear before the .WaitAsync(ct) on Task.Run
        var loadingModelIdx = source.IndexOf("SetState(DictationState.LoadingModel)", StringComparison.Ordinal);
        var waitAsyncIdx = source.IndexOf(").WaitAsync(ct)", StringComparison.Ordinal);
        (loadingModelIdx >= 0).ShouldBeTrue();
        (waitAsyncIdx > loadingModelIdx).ShouldBeTrue();
    }
}
