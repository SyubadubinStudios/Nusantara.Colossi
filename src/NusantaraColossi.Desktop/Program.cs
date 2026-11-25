using Avalonia;
using NusantaraColossi.Game.UI;
using ThreeNet;

namespace NusantaraColossi.Desktop;

/// <summary>
/// Desktop head (Windows, macOS, Linux).
/// Usage: NusantaraColossi [--screenshot &lt;folder&gt;] [--lang en|id]
/// </summary>
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--screenshot" && i + 1 < args.Length) App.ScreenshotFolder = Path.GetFullPath(args[++i]);
            else if (args[i] == "--lang" && i + 1 < args.Length) Environment.SetEnvironmentVariable("NC_SHOT_LANG", args[++i]);
        }

        try
        {
            ThreeNetRuntime.Initialize(LogLevel.Warning);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or ThreeNetException)
        {
            Console.Error.WriteLine($"Three.Net native runtime failed to load: {ex.Message}");
            return 1;
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the Avalonia designer.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software] })
        .LogToTrace();
}
