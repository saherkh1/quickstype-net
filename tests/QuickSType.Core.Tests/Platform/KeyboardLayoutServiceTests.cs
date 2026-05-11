using QuickSType.Core.Platform;

namespace QuickSType.Core.Tests.Platform;

public class KeyboardLayoutServiceTests
{
    // ── InputLayout record ─────────────────────────────────────────

    [Fact]
    public void InputLayout_constructs_with_display_name_and_code()
    {
        var layout = new InputLayout("English (US)", "en-US");
        layout.DisplayName.ShouldBe("English (US)");
        layout.Code.ShouldBe("en-US");
    }

    [Fact]
    public void InputLayout_ToString_returns_display_name()
    {
        var layout = new InputLayout("English (US)", "en-US");
        layout.ToString().ShouldBe("English (US)");
    }

    [Fact]
    public void InputLayout_value_equality_compares_both_fields()
    {
        var a = new InputLayout("English (US)", "en-US");
        var b = new InputLayout("English (US)", "en-US");
        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void InputLayout_not_equal_when_code_differs()
    {
        var a = new InputLayout("English", "en-US");
        var b = new InputLayout("English", "en-GB");
        a.ShouldNotBe(b);
    }

    [Fact]
    public void InputLayout_not_equal_when_display_name_differs()
    {
        var a = new InputLayout("English", "en-US");
        var b = new InputLayout("Hebrew", "en-US");
        a.ShouldNotBe(b);
    }

    [Fact]
    public void InputLayout_handles_empty_code()
    {
        var layout = new InputLayout("—", "");
        layout.DisplayName.ShouldBe("—");
        layout.Code.ShouldBe("");
    }

    [Fact]
    public void InputLayout_handles_arabic_display_name()
    {
        var layout = new InputLayout("Arabic", "ar");
        layout.DisplayName.ShouldBe("Arabic");
        layout.Code.ShouldBe("ar");
    }

    // ── IKeyboardLayoutService via fake ─────────────────────────────

    private sealed class FakeKeyboardLayoutService : IKeyboardLayoutService
    {
        private InputLayout _current;
        public FakeKeyboardLayoutService(InputLayout initial) => _current = initial;
        public InputLayout CurrentLayout => _current;
        public event Action<InputLayout>? LayoutChanged;

        public void SimulateLayoutChange(InputLayout newLayout)
        {
            _current = newLayout;
            LayoutChanged?.Invoke(newLayout);
        }
    }

    [Fact]
    public void CurrentLayout_returns_initial_value()
    {
        var initial = new InputLayout("English (US)", "en-US");
        var svc = new FakeKeyboardLayoutService(initial);
        svc.CurrentLayout.ShouldBe(initial);
    }

    [Fact]
    public void LayoutChanged_fires_with_new_layout()
    {
        var svc = new FakeKeyboardLayoutService(new InputLayout("English (US)", "en-US"));
        InputLayout? received = null;
        svc.LayoutChanged += l => received = l;

        var newLayout = new InputLayout("Arabic", "ar");
        svc.SimulateLayoutChange(newLayout);

        received.ShouldNotBeNull();
        received.Value.ShouldBe(newLayout);
        svc.CurrentLayout.ShouldBe(newLayout);
    }

    [Fact]
    public void LayoutChanged_handles_multiple_subscribers()
    {
        var svc = new FakeKeyboardLayoutService(new InputLayout("English", "en-US"));
        var count = 0;
        svc.LayoutChanged += _ => count++;
        svc.LayoutChanged += _ => count++;

        svc.SimulateLayoutChange(new InputLayout("Hebrew", "he"));

        count.ShouldBe(2);
    }
}
