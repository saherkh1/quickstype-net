// WAVE0 stub — replace InputLayoutStub with InputLayout after 03-02
namespace QuickSType.Core.Tests.Platform;

public readonly record struct InputLayoutStub(string DisplayName, string Code);

public class KeyboardLayoutServiceTests
{
    [Fact]
    public void InputLayout_DisplayName_stores_value()
    {
        var layout = new InputLayoutStub("English (US)", "en-US");
        layout.DisplayName.ShouldBe("English (US)");
        layout.Code.ShouldBe("en-US");
    }

    [Fact]
    public void InputLayout_Code_stores_BCP47_code()
    {
        var layout = new InputLayoutStub("Hebrew", "he-IL");
        layout.Code.ShouldBe("he-IL");
    }

    [Fact]
    public void InputLayout_is_readonly_record()
    {
        var a = new InputLayoutStub("en", "en");
        var b = new InputLayoutStub("en", "en");
        a.ShouldBe(b);
    }
}
