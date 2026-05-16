using System.Reflection;
using Microsoft.Extensions.Logging;
using QuickSType.Core.Config;
using Sentry;

namespace QuickSType.UI.Telemetry;

public sealed class SentryTelemetryService : ITelemetryService
{
    public const string DsnEnvironmentVariable = "QUICKSTYPE_SENTRY_DSN";
    public const string EnvironmentEnvironmentVariable = "QUICKSTYPE_SENTRY_ENVIRONMENT";

    private readonly ILogger? _log;
    private readonly object _gate = new();
    private IDisposable? _sdk;
    private TelemetryOptions _options = new();

    public bool IsEnabled { get; private set; }

    public SentryTelemetryService(TelemetryOptions options, ILogger? log = null)
    {
        _log = log;
        Apply(options);
    }

    public static TelemetryOptions CreateOptions(AppConfig config)
    {
        var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "unknown";

        return new TelemetryOptions
        {
            Enabled = config.EnableCrashTelemetry,
            Dsn = Environment.GetEnvironmentVariable(DsnEnvironmentVariable),
            Environment = Environment.GetEnvironmentVariable(EnvironmentEnvironmentVariable) ?? "production",
            Release = $"QuickSType@{version}",
        };
    }

    public static bool ShouldInitialize(TelemetryOptions options) =>
        options.Enabled && !string.IsNullOrWhiteSpace(options.Dsn);

    public void ApplyConfig(AppConfig config) => Apply(CreateOptions(config));

    public void CaptureException(Exception exception, IReadOnlyDictionary<string, string>? context = null)
    {
        if (!IsEnabled)
        {
            return;
        }

        var safeContext = TelemetryScrubber.Scrub(context)
            .ToDictionary(static kvp => kvp.Key, static kvp => (object)kvp.Value, StringComparer.Ordinal);

        SentrySdk.CaptureException(exception, scope =>
        {
            foreach (var (key, value) in safeContext)
            {
                scope.SetExtra(key, value);
            }
        });
    }

    public void AddBreadcrumb(string message, IReadOnlyDictionary<string, string>? data = null)
    {
        if (!IsEnabled)
        {
            return;
        }

        var safeData = TelemetryScrubber.Scrub(data)
            .ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value, StringComparer.Ordinal);

        SentrySdk.AddBreadcrumb(TelemetryScrubber.Scrub(message), data: safeData);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            IsEnabled = false;
            _sdk?.Dispose();
            _sdk = null;
        }
    }

    public static SentryEvent ScrubEventForTesting(SentryEvent sentryEvent) => ScrubEvent(sentryEvent);

    private void Apply(TelemetryOptions options)
    {
        lock (_gate)
        {
            if (_options == options)
            {
                return;
            }

            _options = options;
            IsEnabled = false;
            _sdk?.Dispose();
            _sdk = null;

            if (!ShouldInitialize(options))
            {
                _log?.LogInformation("Crash telemetry disabled");
                return;
            }

            _sdk = SentrySdk.Init(sentryOptions =>
            {
                sentryOptions.Dsn = options.Dsn;
                sentryOptions.Environment = options.Environment;
                sentryOptions.Release = options.Release;
                sentryOptions.SendDefaultPii = false;
                sentryOptions.AutoSessionTracking = false;
                sentryOptions.TracesSampleRate = 0;
                sentryOptions.SetBeforeSend(static sentryEvent => ScrubEvent(sentryEvent));
            });
            IsEnabled = true;
            _log?.LogInformation("Crash telemetry enabled for {Environment}", options.Environment);
        }
    }

    private static SentryEvent ScrubEvent(SentryEvent sentryEvent)
    {
        foreach (var (key, value) in sentryEvent.Extra.ToArray())
        {
            var scrubbed = TelemetryScrubber.Scrub(new Dictionary<string, object?> { [key] = value });
            sentryEvent.SetExtra(key, scrubbed[key]);
        }

        foreach (var (key, value) in sentryEvent.Tags.ToArray())
        {
            var scrubbed = TelemetryScrubber.Scrub(new Dictionary<string, string> { [key] = value });
            sentryEvent.SetTag(key, scrubbed[key]);
        }

        return sentryEvent;
    }
}
