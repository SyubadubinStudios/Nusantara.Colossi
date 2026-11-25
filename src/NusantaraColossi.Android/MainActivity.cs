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
        // QA: `adb shell touch /sdcard/Android/data/<package>/files/profile` logs frame timings to logcat.
        NusantaraColossi.Game.UI.Shell.Log = line => global::Android.Util.Log.Info("NC", line);
        NusantaraColossi.Game.UI.Shell.ProfileOverride = GetExternalFilesDir(null) is { } ext && File.Exists(Path.Combine(ext.AbsolutePath, "profile"));
        // ThreeNet's device audio aborts on Android; mix offline and stream through AudioTrack instead.
        NusantaraColossi.Game.Audio.GameAudio.OfflineSink = engine => new AndroidAudioStream(engine, NusantaraColossi.Game.Audio.GameAudio.OfflineSampleRate);
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
public sealed class MainActivity : AvaloniaMainActivity
{
    protected override void OnPause()
    {
        AndroidAudioStream.Current?.SetPaused(true);
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        AndroidAudioStream.Current?.SetPaused(false);
    }
}
