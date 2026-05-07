using System.IO;
using System.Linq;
using Shouldly;
using Xunit;

namespace QuickSType.Core.Tests;

public class HardenLoggingRegressionTests
{
    private static readonly string[] InventoryFiles = new[]
    {
        "src/QuickSType.Core/Audio/PortAudioCapture.cs",
        "src/QuickSType.Core/Hotkey/HotkeyService.cs",
        "src/QuickSType.Platform.Mac/MacAutoLaunch.cs",
        "src/QuickSType.Platform.Mac/MacNotifications.cs",
        "src/QuickSType.Platform.Mac/MacPermissions.cs",
        "src/QuickSType.Platform.Windows/WindowsPermissions.cs",
        "src/QuickSType.Platform.Windows/WindowsNotifications.cs",
        "src/QuickSType.UI/Composition/AppHost.cs",
        "src/QuickSType.UI/Tray/TrayService.cs",
        "src/QuickSType.UI/Views/MainWindow.axaml.cs",
    };

    [Fact]
    public void No_empty_catch_swallow_in_HARDEN03_files()
    {
        // HARDEN-03: every empty-catch in the inventory was replaced with structured logging.
        // This test grep-pins the absence of `catch { }` and `/* swallow */` markers.
        foreach (var rel in InventoryFiles)
        {
            var path = LocateRepoFile(rel);
            var src = File.ReadAllText(path);
            // Strip line comments for the next assertion (we want to allow doc-comments
            // that mention "swallow" as historical record but not active swallow patterns).
            var nonComment = string.Join("\n",
                src.Split('\n').Where(l => !l.TrimStart().StartsWith("//")));
            // Look for empty-catch block patterns
            nonComment.ShouldNotMatch(@"catch\s*(\([^)]*\))?\s*\{\s*\}",
                $"{rel}: empty catch block remains; HARDEN-03 inventory says it must log");
            nonComment.ShouldNotMatch(@"catch\s*(\([^)]*\))?\s*\{\s*/\*\s*swallow",
                $"{rel}: '/* swallow */' marker remains; HARDEN-03 says replace with structured log");
        }
    }

    [Fact]
    public void Inventory_files_reference_log_in_each_file()
    {
        // Sanity: each file in the inventory now has at least one log-level call (Log* method).
        foreach (var rel in InventoryFiles)
        {
            var src = File.ReadAllText(LocateRepoFile(rel));
            src.ShouldMatch(@"\.(LogTrace|LogDebug|LogInformation|LogWarning|LogError)\(",
                $"{rel}: no Log* call found; HARDEN-03 inventory says this file must log");
        }
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
