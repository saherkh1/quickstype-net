using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Platform;

namespace QuickSType.Platform.Mac;

// Based on spike report findings (HUD_ANCHOR_MAC.md): NSStatusItem rect is reachable
// via ObjC enumeration of NSApp.windows when AppKit is loaded (guaranteed in Avalonia app).
public sealed partial class MacTrayPositionService : ITrayPositionService
{
    private const string LibObjC = "/usr/lib/libobjc.dylib";

    private readonly ILogger _log;
    private System.Drawing.Rectangle _lastRect;

    public MacTrayPositionService(ILogger<MacTrayPositionService>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
        _lastRect = GetFallbackRect();
    }

    public System.Drawing.Rectangle GetTrayRect()
    {
        try
        {
            var rect = QueryStatusBarWindowFrame();
            if (rect.HasValue)
            {
                _lastRect = rect.Value;
                return _lastRect;
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "NSStatusItem rect query failed, using fallback");
        }

        return GetFallbackRect();
    }

    private static System.Drawing.Rectangle? QueryStatusBarWindowFrame()
    {
        var nsAppCls = objc_getClass("NSApplication");
        if (nsAppCls == IntPtr.Zero) return null;

        var sharedAppSel = sel_getUid("sharedApplication");
        var app = objc_msgSend_IntPtr(nsAppCls, sharedAppSel);
        if (app == IntPtr.Zero) return null;

        var windowsSel = sel_getUid("windows");
        var windows = objc_msgSend_IntPtr(app, windowsSel);
        if (windows == IntPtr.Zero) return null;

        var countSel = sel_getUid("count");
        var count = objc_msgSend_UInt64(windows, countSel);

        var objectAtIndexSel = sel_getUid("objectAtIndex:");
        var classNameSel = sel_getUid("className");
        var frameSel = sel_getUid("frame");

        for (nuint i = 0; i < count; i++)
        {
            var window = objc_msgSend_IntPtr_IntPtr(windows, objectAtIndexSel, (IntPtr)i);
            if (window == IntPtr.Zero) continue;

            var className = Marshal.PtrToStringAuto(objc_msgSend_IntPtr(window, classNameSel));
            if (className != null && className.Contains("StatusBar"))
            {
                objc_msgSend_Stret(out var frame, window, frameSel);
                // NSWindow.frame is in screen coordinates (bottom-left origin).
                // System.Drawing.Rectangle uses top-left origin, but for the HUD
                // we pass the raw screen-coordinate rect to Phase 5's anchor logic.
                return new System.Drawing.Rectangle(
                    (int)frame.X, (int)frame.Y,
                    (int)frame.Width, (int)frame.Height);
            }
        }

        return null;
    }

    private static System.Drawing.Rectangle GetFallbackRect()
    {
        // D-12 Mac: top-right of primary screen, 20px from right, 4px below menu bar
        var nsScreenCls = objc_getClass("NSScreen");
        if (nsScreenCls == IntPtr.Zero)
            return new System.Drawing.Rectangle(1860, 4, 1, 1);

        var mainScreenSel = sel_getUid("mainScreen");
        var mainScreen = objc_msgSend_IntPtr(nsScreenCls, mainScreenSel);
        if (mainScreen == IntPtr.Zero)
            return new System.Drawing.Rectangle(1860, 4, 1, 1);

        objc_msgSend_Stret(out var visibleFrame, mainScreen, sel_getUid("visibleFrame"));
        return new System.Drawing.Rectangle(
            (int)(visibleFrame.X + visibleFrame.Width - 20),
            (int)(visibleFrame.Y + visibleFrame.Height + 4),
            1, 1);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;
    }

    [DllImport(LibObjC, EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(LibObjC, EntryPoint = "sel_getUid")]
    private static extern IntPtr sel_getUid(string name);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern ulong objc_msgSend_UInt64(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Stret(out CGRect rect, IntPtr receiver, IntPtr selector);
}
