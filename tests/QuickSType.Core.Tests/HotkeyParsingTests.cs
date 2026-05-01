using QuickSType.Core.Hotkey;
using SharpHook.Data;

namespace QuickSType.Core.Tests;

public class HotkeyParsingTests
{
    [Theory]
    [InlineData("VcRightAlt", KeyCode.VcRightAlt)]
    [InlineData("vcrightalt", KeyCode.VcRightAlt)]
    [InlineData("VcF13", KeyCode.VcF13)]
    [InlineData("VcLeftMeta", KeyCode.VcLeftMeta)]
    public void Parses_known_keys(string name, KeyCode expected)
    {
        HotkeyService.ParseKey(name).ShouldBe(expected);
    }

    [Theory]
    [InlineData("not-a-key")]
    [InlineData("")]
    public void Defaults_unknown_to_right_alt(string name)
    {
        HotkeyService.ParseKey(name).ShouldBe(KeyCode.VcRightAlt);
    }

    [Fact]
    public void Format_round_trips_parse()
    {
        var key = HotkeyService.ParseKey("VcF5");
        HotkeyService.Format(key).ShouldBe("VcF5");
    }
}
