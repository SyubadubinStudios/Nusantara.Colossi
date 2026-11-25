using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using NusantaraColossi.Game;
using App = NusantaraColossi.Game.UI.App;

namespace NusantaraColossi.Android;

/// <summary>Android application: unpacks runtime assets, then starts the shared Avalonia app.</summary>
[Application(Label = "Nusantara Colossi", Icon = "@mipmap/appicon")]
public sealed class AndroidApp : AvaloniaAndroidApplication<App>
{
    public AndroidApp(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer) { }

    public override void OnCreate()
    {
        App.IsMobile = true;
        var root = Path.Combine(FilesDir!.AbsolutePath, "assets");
        AssetExtractor.Extract(Assets!, root, "assets", ApplicationContext!.PackageManager!.GetPackageInfo(PackageName!, 0)!.LongVersionCode);
        AssetPaths.Root = root;
        GameSettings.Directory = Path.Combine(FilesDir.AbsolutePath, "save");
        base.OnCreate();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).With(new AndroidPlatformOptions { RenderingMode = [AndroidRenderingMode.Egl, AndroidRenderingMode.Software] });
}

[Activity(
    Label = "Nusantara Colossi",
    Theme = "@style/NusantaraTheme",
    MainLauncher = true,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.KeyboardHidden | ConfigChanges.Keyboard)]
public sealed class MainActivity : AvaloniaMainActivity;
