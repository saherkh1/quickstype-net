using QuickSType.UI.Telemetry;
using Sentry;

namespace QuickSType.UI.SmokeTest;

public static class TelemetrySmokeHarness
{
    public static int Run()
    {
        var options = new TelemetryOptions
        {
            Enabled = true,
            Dsn = Environment.GetEnvironmentVariable(SentryTelemetryService.DsnEnvironmentVariable),
            Environment = Environment.GetEnvironmentVariable(SentryTelemetryService.EnvironmentEnvironmentVariable) ?? "release-smoke",
            Release = "QuickSType@telemetry-smoke",
        };

        if (!SentryTelemetryService.ShouldInitialize(options))
        {
            Console.Error.WriteLine($"Telemetry smoke test requires {SentryTelemetryService.DsnEnvironmentVariable}.");
            return 64;
        }

        using var service = new SentryTelemetryService(options);
        if (!service.IsEnabled)
        {
            Console.Error.WriteLine("Telemetry smoke test failed to initialize Sentry.");
            return 1;
        }

        service.AddBreadcrumb(
            "telemetry smoke transcript alice@example.com",
            new Dictionary<string, string>
            {
                ["transcript_preview"] = "email alice@example.com private launch notes",
                ["model_path"] = "/Users/saherk/Library/Application Support/QuickSType/models/ggml-small.bin",
            });

        service.CaptureException(
            new InvalidOperationException("QuickSType telemetry smoke test"),
            new Dictionary<string, string>
            {
                ["audio_device"] = "Saher's AirPods Pro",
                ["safe_marker"] = "telemetry-smoke",
            });

        SentrySdk.FlushAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        Console.WriteLine("Telemetry smoke event submitted. Check Sentry for safe_marker=telemetry-smoke.");
        return 0;
    }
}
