using QuickSType.Core;
using QuickSType.Core.Config;
using QuickSType.Core.Platform;
using QuickSType.Core.Transcribe;
using Xunit;
using Shouldly;

namespace QuickSType.Core.Tests.Streaming;

public class LanguageHintResolutionTests
{
    private sealed class FakeLayout : IKeyboardLayoutService
    {
#pragma warning disable CS0067
        public event Action<InputLayout>? LayoutChanged;
#pragma warning restore CS0067

        private readonly InputLayout _layout;

        public FakeLayout(string bcp47Code)
        {
            _layout = new InputLayout(bcp47Code, bcp47Code);
        }

        public InputLayout CurrentLayout => _layout;
    }

    [Fact]
    public void LayoutOverride_ignored_when_KeyboardLayoutDriven_false()
    {
        var engine = AutoDegradeTests.NewEngine(
            streamer: null,
            specs: null,
            streamingMode: "auto",
            keyboardLayout: new FakeLayout("fr-FR"),
            config: new AppConfig
            {
                ActiveLanguage = "en",
                AutoLanguage = false,
                KeyboardLayoutDriven = false,
                StreamingMode = "auto",
            });

        var resolved = engine.ApplyLayoutLanguageHint(engine.Config);

        resolved.ActiveLanguage.ShouldBe("en");
    }

    [Fact]
    public void LayoutOverride_applies_when_KeyboardLayoutDriven_true()
    {
        var engine = AutoDegradeTests.NewEngine(
            streamer: null,
            specs: null,
            streamingMode: "auto",
            keyboardLayout: new FakeLayout("fr-FR"),
            config: new AppConfig
            {
                ActiveLanguage = "en",
                AutoLanguage = false,
                KeyboardLayoutDriven = true,
                StreamingMode = "auto",
            });

        var resolved = engine.ApplyLayoutLanguageHint(engine.Config);

        resolved.ActiveLanguage.ShouldBe("fr");
    }
}
