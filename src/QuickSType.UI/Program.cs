using Avalonia;
using QuickSType.UI.SmokeTest;

namespace QuickSType.UI;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // HARDEN-05: hidden CI smoke-test mode. Short-circuits BEFORE Avalonia bootstrap
        // so hosted GH runners (no display server) don't try to spin up the UI.
        if (args.Length > 0 && args[0] == "--smoke-test")
            return SmokeHarness.Run(args.AsSpan(1).ToArray());

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal: {ex}");
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
