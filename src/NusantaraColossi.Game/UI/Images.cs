using Avalonia.Media.Imaging;

namespace NusantaraColossi.Game.UI;

/// <summary>Decoded-once cache for 2D art (portraits, key art). Missing files return null.</summary>
public static class Images
{
    private static readonly Dictionary<string, Bitmap?> cache = [];

    public static Bitmap? Load(string path, int decodeWidth = 0)
    {
        var key = path + "@" + decodeWidth;
        if (cache.TryGetValue(key, out var bitmap)) return bitmap;
        try
        {
            if (!File.Exists(path)) bitmap = null;
            else
            {
                using var stream = File.OpenRead(path);
                // Decode straight to display size: a 2K portrait shown at 380 px wastes ~15 MB otherwise.
                bitmap = decodeWidth > 0 ? Bitmap.DecodeToWidth(stream, decodeWidth, BitmapInterpolationMode.HighQuality) : new Bitmap(stream);
            }
        }
        catch (Exception) { bitmap = null; }
        cache[key] = bitmap;
        return bitmap;
    }
}
