using System.Reflection;
using System.Runtime.InteropServices;
using QuickSType.Core.Platform;

namespace QuickSType.UI.Updates;

public sealed class PostUpdateSelfCheckService
{
    private readonly string _markerPath;
    private readonly Func<bool> _isMacOs;
    private readonly Func<string> _currentVersion;

    public PostUpdateSelfCheckService(
        string markerPath,
        Func<bool>? isMacOs = null,
        Func<string>? currentVersion = null)
    {
        _markerPath = markerPath;
        _isMacOs = isMacOs ?? (() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX));
        _currentVersion = currentVersion ?? GetAssemblyVersion;
    }

    public static PostUpdateSelfCheckService CreateDefault()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? Environment.SpecialFolder.LocalApplicationData
                    : Environment.SpecialFolder.ApplicationData),
            "QuickSType");
        return new PostUpdateSelfCheckService(Path.Combine(dir, "post-update-self-check.txt"));
    }

    public PostUpdateSelfCheckResult Evaluate(IPermissionService permissions)
    {
        var version = _currentVersion();
        if (!_isMacOs())
        {
            return PostUpdateSelfCheckResult.None(version);
        }

        if (permissions.HasAccessibilityAccess())
        {
            WriteMarker(new SelfCheckMarker(version, Dismissed: false));
            return PostUpdateSelfCheckResult.None(version);
        }

        var marker = ReadMarker();
        if (marker.Version == version && marker.Dismissed)
        {
            return PostUpdateSelfCheckResult.None(version);
        }

        return PostUpdateSelfCheckResult.AccessibilityMissing(version);
    }

    public void Dismiss(string version)
    {
        WriteMarker(new SelfCheckMarker(version, Dismissed: true));
    }

    private SelfCheckMarker ReadMarker()
    {
        try
        {
            if (!File.Exists(_markerPath)) return default;
            var lines = File.ReadAllLines(_markerPath);
            return new SelfCheckMarker(
                lines.Length > 0 ? lines[0] : string.Empty,
                lines.Length > 1 && bool.TryParse(lines[1], out var dismissed) && dismissed);
        }
        catch
        {
            return default;
        }
    }

    private void WriteMarker(SelfCheckMarker marker)
    {
        try
        {
            var dir = Path.GetDirectoryName(_markerPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllLines(_markerPath, [marker.Version, marker.Dismissed.ToString()]);
        }
        catch
        {
            // Self-check persistence is best effort; never block app startup.
        }
    }

    private static string GetAssemblyVersion()
    {
        return typeof(PostUpdateSelfCheckService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(PostUpdateSelfCheckService).Assembly.GetName().Version?.ToString(3)
            ?? "unknown";
    }

    private readonly record struct SelfCheckMarker(string Version, bool Dismissed);
}

public enum PostUpdateSelfCheckKind
{
    None,
    AccessibilityMissing,
}

public sealed record PostUpdateSelfCheckResult(
    PostUpdateSelfCheckKind Kind,
    string Version,
    string? Message = null)
{
    public static PostUpdateSelfCheckResult None(string version) =>
        new(PostUpdateSelfCheckKind.None, version);

    public static PostUpdateSelfCheckResult AccessibilityMissing(string version) =>
        new(
            PostUpdateSelfCheckKind.AccessibilityMissing,
            version,
            "Accessibility permission is required after this update.");
}
