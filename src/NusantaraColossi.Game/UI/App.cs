using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace NusantaraColossi.Game.UI;

/// <summary>Avalonia application shared by the desktop and Android heads.</summary>
public sealed class App : Application
{
    /// <summary>Set by the platform head before start-up.</summary>
    public static bool IsMobile { get; set; }

    /// <summary>When set, the app tours its screens, writes PNGs here, then exits.</summary>
    public static string? ScreenshotFolder { get; set; }

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
        // Fluent's sliders, toggles and focus rings follow the accent: make it prada gold, not Windows blue.
        Resources["SystemAccentColor"] = Theme.PradaColor;
        Resources["SystemAccentColorLight1"] = Theme.PradaBrightColor;
        Resources["SystemAccentColorLight2"] = Theme.PradaBrightColor;
        Resources["SystemAccentColorLight3"] = Theme.PradaBrightColor;
        Resources["SystemAccentColorDark1"] = Theme.PradaColor;
        Resources["SystemAccentColorDark2"] = Theme.PradaColor;
        Resources["SystemAccentColorDark3"] = Theme.PradaColor;
        Name = "Nusantara Colossi";
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var context = new GameContext(IsMobile) { ScreenshotMode = ScreenshotFolder is not null };
        var shell = new Shell(context);
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                var window = new Window
                {
                    Title = "Nusantara Colossi",
                    Content = shell,
                    Width = 1600,
                    Height = 900,
                    MinWidth = 960,
                    MinHeight = 560,
                    Background = Theme.Andesite,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                };
                var icon = AssetPaths.Art("icon.png");
                if (File.Exists(icon))
                {
                    try { window.Icon = new WindowIcon(new Bitmap(icon)); }
                    catch (Exception) { /* Cosmetic. */ }
                }
                window.Closing += (_, _) => shell.Dispose();
                desktop.MainWindow = window;
                if (ScreenshotFolder is { } folder) ScreenshotTour.Start(shell, folder, () => desktop.Shutdown());
                break;
            case ISingleViewApplicationLifetime single:
                single.MainView = shell;
                break;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
