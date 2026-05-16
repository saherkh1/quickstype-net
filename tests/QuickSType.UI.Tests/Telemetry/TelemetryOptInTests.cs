using QuickSType.Core.Config;
using QuickSType.UI.Telemetry;
using Sentry;

namespace QuickSType.UI.Tests.Telemetry;

public sealed class TelemetryOptInTests
{
    [Fact]
    public void Default_config_keeps_telemetry_disabled()
    {
        new AppConfig().EnableCrashTelemetry.ShouldBeFalse();
    }

    [Fact]
    public void Disabled_options_do_not_initialize_even_with_dsn()
    {
        using var service = new SentryTelemetryService(new TelemetryOptions
        {
            Enabled = false,
            Dsn = "https://public@example.com/1",
        });

        service.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Enabled_options_without_dsn_do_not_initialize()
    {
        using var service = new SentryTelemetryService(new TelemetryOptions
        {
            Enabled = true,
            Dsn = null,
        });

        service.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Enabled_options_with_dsn_initialize()
    {
        using var service = new SentryTelemetryService(new TelemetryOptions
        {
            Enabled = true,
            Dsn = "https://public@example.com/1",
            Release = "QuickSType@test",
        });

        service.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Create_options_reads_dsn_from_environment_but_uses_config_for_opt_in()
    {
        var previousDsn = Environment.GetEnvironmentVariable(SentryTelemetryService.DsnEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(SentryTelemetryService.DsnEnvironmentVariable, "https://public@example.com/1");

            var options = SentryTelemetryService.CreateOptions(new AppConfig { EnableCrashTelemetry = true });

            options.Enabled.ShouldBeTrue();
            options.Dsn.ShouldBe("https://public@example.com/1");
            SentryTelemetryService.ShouldInitialize(options).ShouldBeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable(SentryTelemetryService.DsnEnvironmentVariable, previousDsn);
        }
    }

    [Fact]
    public void Before_send_scrubs_extra_and_tags()
    {
        var sentryEvent = new SentryEvent();
        sentryEvent.SetExtra("transcript_preview", "send alice@example.com the private note");
        sentryEvent.SetExtra("duration_ms", 250);
        sentryEvent.SetTag("model_path", "/Users/saherk/models/ggml-small.bin");
        sentryEvent.SetTag("mode", "streaming");

        var scrubbed = SentryTelemetryService.ScrubEventForTesting(sentryEvent);

        scrubbed.Extra["transcript_preview"].ShouldBe("[redacted]");
        scrubbed.Extra["duration_ms"].ShouldBe(250);
        scrubbed.Tags["model_path"].ShouldBe("[redacted]");
        scrubbed.Tags["mode"].ShouldBe("streaming");
    }
}
