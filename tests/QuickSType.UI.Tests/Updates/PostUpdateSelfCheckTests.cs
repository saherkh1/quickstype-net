using QuickSType.Core.Platform;
using QuickSType.UI.Updates;

namespace QuickSType.UI.Tests.Updates;

public class PostUpdateSelfCheckTests
{
    [Fact]
    public void Missing_accessibility_on_macos_shows_banner()
    {
        using var temp = new TempMarker();
        var service = temp.Create(isMacOs: true, version: "0.99.1");
        var permissions = new FakePermissions(accessibility: false);

        var result = service.Evaluate(permissions);

        result.Kind.ShouldBe(PostUpdateSelfCheckKind.AccessibilityMissing);
        result.Message.ShouldBe("Accessibility permission is required after this update.");
    }

    [Fact]
    public void Permission_present_on_macos_is_noop_and_marks_version_checked()
    {
        using var temp = new TempMarker();
        var service = temp.Create(isMacOs: true, version: "0.99.1");
        var permissions = new FakePermissions(accessibility: true);

        var result = service.Evaluate(permissions);

        result.Kind.ShouldBe(PostUpdateSelfCheckKind.None);
        File.ReadAllLines(temp.Path)[0].ShouldBe("0.99.1");
    }

    [Fact]
    public void Dismiss_hides_banner_for_same_version()
    {
        using var temp = new TempMarker();
        var service = temp.Create(isMacOs: true, version: "0.99.1");
        var permissions = new FakePermissions(accessibility: false);

        service.Evaluate(permissions).Kind.ShouldBe(PostUpdateSelfCheckKind.AccessibilityMissing);
        service.Dismiss("0.99.1");
        var result = service.Evaluate(permissions);

        result.Kind.ShouldBe(PostUpdateSelfCheckKind.None);
    }

    [Fact]
    public void New_version_shows_banner_even_if_previous_version_was_dismissed()
    {
        using var temp = new TempMarker();
        var service = temp.Create(isMacOs: true, version: "0.99.1");
        var permissions = new FakePermissions(accessibility: false);
        service.Dismiss("0.99.0");

        var result = service.Evaluate(permissions);

        result.Kind.ShouldBe(PostUpdateSelfCheckKind.AccessibilityMissing);
    }

    [Fact]
    public void Non_macos_is_noop()
    {
        using var temp = new TempMarker();
        var service = temp.Create(isMacOs: false, version: "0.99.1");
        var permissions = new FakePermissions(accessibility: false);

        var result = service.Evaluate(permissions);

        result.Kind.ShouldBe(PostUpdateSelfCheckKind.None);
        File.Exists(temp.Path).ShouldBeFalse();
    }

    private sealed class TempMarker : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "quickstype-self-check-" + Guid.NewGuid().ToString("N") + ".txt");

        public PostUpdateSelfCheckService Create(bool isMacOs, string version)
        {
            return new PostUpdateSelfCheckService(Path, () => isMacOs, () => version);
        }

        public void Dispose()
        {
            if (File.Exists(Path)) File.Delete(Path);
        }
    }

    private sealed class FakePermissions : IPermissionService
    {
        private readonly bool _accessibility;

        public FakePermissions(bool accessibility)
        {
            _accessibility = accessibility;
        }

        public bool HasMicrophoneAccess() => true;
        public bool HasInputMonitoringAccess() => true;
        public bool HasAccessibilityAccess() => _accessibility;
        public void OpenMicrophoneSettings() { }
        public void OpenInputMonitoringSettings() { }
        public void OpenAccessibilitySettings() { }
        public void RequestAccessibilityIfNeeded() { }
    }
}
