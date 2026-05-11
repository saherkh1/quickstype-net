namespace QuickSType.Core.Platform;

/// <summary>
/// Snapshot of the OS theme mode and accent color, sourced from <see cref="ISystemThemeService"/>.
/// </summary>
/// <param name="IsDark">True when OS is in dark mode, false for light mode.</param>
/// <param name="AccentColorHex">OS accent color as #RRGGBB hex string (no alpha). Reserved for Phase 5 HUD translucent tinting.</param>
public readonly record struct AppTheme(bool IsDark, string AccentColorHex);
