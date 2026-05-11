namespace QuickSType.Core.Platform;

/// <summary>
/// Port for retrieving the tray icon's bounding rectangle in screen coordinates.
/// The service caches the last known rect internally and re-queries on each call (D-13).
/// </summary>
public interface ITrayPositionService
{
    /// <summary>
    /// Returns the tray icon bounding rectangle in screen coordinates, or a fallback rect
    /// if the tray icon position cannot be determined. The service caches the last known
    /// rect internally (D-13).
    /// </summary>
    /// <remarks>
    /// Mac fallback (D-12): top-right of primary screen's working area, 20px from right edge,
    /// 4px below menu bar.
    /// Windows 11 fallback (D-12): bottom-center of primary monitor working area, 40px
    /// from bottom edge.
    /// </remarks>
    System.Drawing.Rectangle GetTrayRect();
}
