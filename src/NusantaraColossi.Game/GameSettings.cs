using System.Text.Json;
using System.Text.Json.Serialization;

namespace NusantaraColossi.Game;

public sealed class GameSettings
{
    public string Language { get; set; } = "id";
    /// <summary>0 = low, 1 = balanced, 2 = cinematic.</summary>
    public int Quality { get; set; } = 1;
    public float Volume { get; set; } = .8f;
    public float MusicVolume { get; set; } = .55f;
    public float Sensitivity { get; set; } = 1f;
    public bool InvertY { get; set; }
    public float CameraShake { get; set; } = 1f;
    public bool Subtitles { get; set; } = true;
    public int FpsCap { get; set; } = 60;
    public string CoopAddress { get; set; } = "127.0.0.1";
    public int CoopPort { get; set; } = 27045;
    /// <summary>False until the player picks a quality; until then the GPU decides on first launch.</summary>
    public bool QualityChosen { get; set; }
    /// <summary>Dynamic resolution: lowers the render scale to hold the FPS cap on weak GPUs.</summary>
    public bool DynamicResolution { get; set; } = true;

    // Accessibility (PLAN.md Milestone 4).
    /// <summary>Action name → Avalonia key name. Missing entries use the defaults in <see cref="UI.InputMapper"/>.</summary>
    public Dictionary<string, string> KeyBindings { get; set; } = [];
    /// <summary>Action name → ThreeNet gamepad button name. Missing entries use the defaults.</summary>
    public Dictionary<string, string> PadBindings { get; set; } = [];
    /// <summary>Grip latches on one press and releases on the next, for players who cannot hold a button.</summary>
    public bool GripToggle { get; set; }
    /// <summary>Brighter secondary text and outlines, more opaque HUD backing, brighter danger colour.</summary>
    public bool HighContrastHud { get; set; }
    /// <summary>Scales every screen and the HUD (0.85–1.3).</summary>
    public float UiScale { get; set; } = 1f;

    /// <summary>Bumped whenever bindings change so the input mapper can refresh its tables lazily.</summary>
    [JsonIgnore] public int BindingsVersion { get; set; }

    /// <summary>The settings instance the running game uses (set by <see cref="Load"/>).</summary>
    [JsonIgnore] public static GameSettings Active { get; private set; } = new();

    [JsonIgnore] public bool IsFirstRun { get; private set; }

    [JsonIgnore] public static string Directory { get; set; } = DefaultDirectory();

    /// <summary>NC_DATA_DIR overrides where settings and saves live (tests, screenshot tours).</summary>
    private static string DefaultDirectory() =>
        Environment.GetEnvironmentVariable("NC_DATA_DIR") is { Length: > 0 } custom ? custom : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Syubadubin Studios", "Nusantara Colossi");

    private static string FilePath => Path.Combine(Directory, "settings.json");

    public static GameSettings Load()
    {
        var settings = LoadFile();
        settings.KeyBindings ??= [];
        settings.PadBindings ??= [];
        settings.UiScale = Math.Clamp(settings.UiScale <= 0 ? 1f : settings.UiScale, .85f, 1.3f);
        Active = settings;
        UI.Theme.ApplyAccessibility(settings);
        return settings;
    }

    private static GameSettings LoadFile()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize(File.ReadAllText(FilePath), SettingsJson.Default.GameSettings) ?? new GameSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { }
        return new GameSettings { IsFirstRun = true };
    }

    public void Save()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, SettingsJson.Default.GameSettings));
            File.Move(temp, FilePath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}

[JsonSerializable(typeof(GameSettings))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class SettingsJson : JsonSerializerContext;

/// <summary>Where runtime assets live. Desktop: next to the executable. Android: extracted to app storage.</summary>
public static class AssetPaths
{
    public static string Root { get; set; } = Path.Combine(AppContext.BaseDirectory, "assets");

    public static string Model(string name) => Path.Combine(Root, "models", name + ".glb");
    public static string Head(string colossusId) => Path.Combine(Root, "models", "heads", colossusId + ".glb");
    /// <summary>The far / low-quality head (about 6k triangles, 512 px textures; see process_lods.py).</summary>
    public static string HeadLod(string colossusId) => Path.Combine(Root, "models", "heads", "lod1", colossusId + ".glb");
    public static string Audio(string name) => Path.Combine(Root, "audio", name + ".wav");
    public static string Voice(string name) => Path.Combine(Root, "audio", "voice", name + ".wav");
    public static string Art(string name) => Path.Combine(Root, "art", name);
    public static string ColossusArt(string colossusId) => Path.Combine(Root, "art", "colossi", colossusId + ".png");
}
