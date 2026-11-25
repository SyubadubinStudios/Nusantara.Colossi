using Android.Content.Res;

namespace NusantaraColossi.Android;

/// <summary>
/// APK assets are compressed entries, not files; ThreeNet's loaders take file paths, so assets are
/// copied to app storage once per app version (a marker file records which version is unpacked).
/// </summary>
internal static class AssetExtractor
{
    public static void Extract(AssetManager assets, string destination, string apkFolder, long version)
    {
        var marker = Path.Combine(destination, ".version");
        if (File.Exists(marker) && File.ReadAllText(marker) == version.ToString()) return;
        Copy(assets, apkFolder, destination);
        File.WriteAllText(marker, version.ToString());
    }

    private static void Copy(AssetManager assets, string source, string destination)
    {
        var entries = assets.List(source) ?? [];
        if (entries.Length == 0)
        {
            // A file (directories list their children).
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var input = assets.Open(source);
            using var output = File.Create(destination);
            input.CopyTo(output, 81920);
            return;
        }
        Directory.CreateDirectory(destination);
        foreach (var entry in entries) Copy(assets, source + "/" + entry, Path.Combine(destination, entry));
    }
}
