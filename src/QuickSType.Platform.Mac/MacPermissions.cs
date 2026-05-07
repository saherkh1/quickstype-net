using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Platform;

namespace QuickSType.Platform.Mac;

public sealed partial class MacPermissions : IPermissionService
{
    private readonly ILogger _log;

    public MacPermissions(ILogger<MacPermissions>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
    }

    public bool HasMicrophoneAccess() => true;

    public bool HasInputMonitoringAccess() => true;

    public bool HasAccessibilityAccess()
    {
        try
        {
            return AXIsProcessTrusted();
        }
        catch
        {
            return false;
        }
    }

    public void RequestAccessibilityIfNeeded()
    {
        try
        {
            using var dictKeys = new CFStringHandle("AXTrustedCheckOptionPrompt");
            var promptPtr = CFBooleanTrue();
            var keys = new IntPtr[] { dictKeys.Handle };
            var values = new IntPtr[] { promptPtr };
            var dict = CFDictionaryCreate(IntPtr.Zero, keys, values, 1, IntPtr.Zero, IntPtr.Zero);
            try { AXIsProcessTrustedWithOptions(dict); }
            finally { if (dict != IntPtr.Zero) CFRelease(dict); }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "AXIsProcessTrustedWithOptions threw; opening Accessibility settings");
            OpenAccessibilitySettings();
        }
    }

    public void OpenMicrophoneSettings() =>
        OpenUrl("x-apple.systempreferences:com.apple.preference.security?Privacy_Microphone");

    public void OpenInputMonitoringSettings() =>
        OpenUrl("x-apple.systempreferences:com.apple.preference.security?Privacy_ListenEvent");

    public void OpenAccessibilitySettings() =>
        OpenUrl("x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility");

    private void OpenUrl(string url)
    {
        try
        {
            var psi = new ProcessStartInfo("/usr/bin/open", url) { UseShellExecute = false };
            Process.Start(psi);
        }
        catch (Exception ex) { _log.LogWarning(ex, "Failed to open Privacy URL {Url}", url); }
    }

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices", EntryPoint = "AXIsProcessTrusted")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool AXIsProcessTrusted();

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices", EntryPoint = "AXIsProcessTrustedWithOptions")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool AXIsProcessTrustedWithOptions(IntPtr options);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", EntryPoint = "CFBooleanGetTypeID")]
    private static partial nuint CFBooleanGetTypeID();

    private static IntPtr CFBooleanTrue()
    {
        var sym = NativeLibrary.Load("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation");
        return NativeLibrary.GetExport(sym, "kCFBooleanTrue");
    }

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial IntPtr CFDictionaryCreate(
        IntPtr allocator, IntPtr[] keys, IntPtr[] values, nint numValues,
        IntPtr keyCallBacks, IntPtr valueCallBacks);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial void CFRelease(IntPtr cf);

    private sealed class CFStringHandle : IDisposable
    {
        public IntPtr Handle { get; }

        public CFStringHandle(string s)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    Handle = CFStringCreateWithBytes(IntPtr.Zero, (IntPtr)ptr, bytes.Length, 0x08000100u, false);
                }
            }
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero) CFRelease(Handle);
        }

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern IntPtr CFStringCreateWithBytes(IntPtr alloc, IntPtr bytes, nint length, uint encoding, [MarshalAs(UnmanagedType.I1)] bool isExternalRepresentation);
    }
}
