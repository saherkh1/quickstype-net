namespace QuickSType.UI.Telemetry;

public sealed record TelemetryOptions
{
    public bool Enabled { get; init; }

    public string? Dsn { get; init; }

    public string Environment { get; init; } = "production";

    public string Release { get; init; } = "QuickSType@unknown";
}
