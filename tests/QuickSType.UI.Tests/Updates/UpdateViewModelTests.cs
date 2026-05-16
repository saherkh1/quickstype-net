using QuickSType.Core;
using QuickSType.UI.Updates;

namespace QuickSType.UI.Tests.Updates;

public class UpdateViewModelTests
{
    [Fact]
    public void Up_to_date_maps_to_enabled_check_action()
    {
        var status = new UpdateStatus(UpdateStatusKind.UpToDate, "0.99.0", "canary");

        var ui = UpdatePresentation.FromStatus(status);

        ui.TrayHeader.ShouldBe("Check for Updates...");
        ui.SettingsStatus.ShouldBe("QuickSType is up to date.");
        ui.ActionText.ShouldBe("Check for Updates");
        ui.IsActionEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Checking_state_disables_actions()
    {
        var status = new UpdateStatus(UpdateStatusKind.UpToDate, "0.99.0", "canary");

        var ui = UpdatePresentation.FromStatus(status, isBusy: true);

        ui.TrayHeader.ShouldBe("Checking for Updates...");
        ui.ActionText.ShouldBe("Checking...");
        ui.IsActionEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Update_ready_is_disabled_while_recording()
    {
        var status = new UpdateStatus(UpdateStatusKind.UpdateReadyToRestart, "0.99.0", "canary");

        var ui = UpdatePresentation.FromStatus(status, dictationState: DictationState.Recording);

        ui.TrayHeader.ShouldBe("Install Update and Restart");
        ui.ActionText.ShouldBe("Install and Restart");
        ui.IsActionEnabled.ShouldBeFalse();
    }

    [Theory]
    [InlineData("homebrew", "Updates managed by Homebrew")]
    [InlineData("winget", "Updates managed by Winget")]
    public void Managed_installs_show_package_manager_state(string manager, string expected)
    {
        var status = new UpdateStatus(
            UpdateStatusKind.ManagedByPackageManager,
            "0.99.0",
            "canary",
            ManagedPackageManager: manager);

        var ui = UpdatePresentation.FromStatus(status);

        ui.TrayHeader.ShouldBe(expected);
        ui.IsActionEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Error_state_keeps_retry_enabled()
    {
        var status = new UpdateStatus(UpdateStatusKind.Error, "0.99.0", "canary", Message: "network down");

        var ui = UpdatePresentation.FromStatus(status);

        ui.TrayHeader.ShouldBe("Check for Updates...");
        ui.SettingsStatus.ShouldContain("network down");
        ui.IsActionEnabled.ShouldBeTrue();
    }
}
