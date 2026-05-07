using System.Text.Json;
using QuickSType.Core.Config;

namespace QuickSType.Core.Tests;

public class HotkeyDefaultTests
{
    [Fact]
    public void New_AppConfig_defaults_hotkey_to_right_ctrl()
    {
        // HARDEN-02 / D-Q3: Windows international layouts collide with Right-Alt (AltGr).
        // Right-Ctrl is the new cross-platform default. Mac is unaffected operationally.
        new AppConfig().Hotkey.ShouldBe("VcRightCtrl");
    }

    [Fact]
    public void Default_serializes_to_json_with_VcRightCtrl()
    {
        var json = JsonSerializer.Serialize(new AppConfig(), ConfigJsonContext.Default.AppConfig);
        json.ShouldContain("\"hotkey\": \"VcRightCtrl\"");
    }

    [Fact]
    public void No_per_platform_default_split()
    {
        // Per RESEARCH.md Pitfall 5: a single-value record should not carry a runtime OS branch.
        // This test pins the architectural decision via a source grep.
        var src = File.ReadAllText(LocateRepoFile("src/QuickSType.Core/Config/Config.cs"));
        src.ShouldNotContain("RuntimeInformation.IsOSPlatform", Case.Sensitive);
        src.ShouldNotContain("OperatingSystem.IsWindows", Case.Sensitive);
    }

    private static string LocateRepoFile(string relativePath)
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        throw new FileNotFoundException($"Could not locate {relativePath} from {AppContext.BaseDirectory}");
    }
}
