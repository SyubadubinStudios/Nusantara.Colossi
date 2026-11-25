using NusantaraColossi.Core;
using NusantaraColossi.Game.Audio;
using ThreeNet;

namespace NusantaraColossi.Game.UI;

/// <summary>Long-lived services shared by every screen.</summary>
public sealed class GameContext : IDisposable
{
    public GameSettings Settings { get; }
    public SaveStore Store { get; }
    public Profile Profile { get; private set; }
    public string? SaveWarning { get; }
    public GameAudio Audio { get; }
    public Gamepads? Pads { get; }
    public InputMapper Input { get; } = new();
    public bool IsMobile { get; init; }
    public bool ScreenshotMode { get; init; }

    public GameContext(bool isMobile = false)
    {
        IsMobile = isMobile;
        Settings = GameSettings.Load();
        if (isMobile && Settings.Quality > 1) Settings.Quality = 1;
        if (!Settings.QualityChosen) Settings.Quality = DetectQuality(isMobile);
        Loc.Language = Settings.Language;
        Store = new SaveStore(GameSettings.Directory);
        Profile = Store.Load(out var warning);
        SaveWarning = warning;
        Audio = new GameAudio(Settings);
        Theme.Sound = name => Audio.Play(name, .45f);
        try { Pads = new Gamepads(); }
        catch (Exception) { Pads = null; } // No HID backend on this platform.
    }

    /// <summary>
    /// Picks a starting quality from the GPU class with a throwaway offscreen renderer:
    /// integrated / mobile GPUs start on Low, discrete ones on Balanced.
    /// </summary>
    private static int DetectQuality(bool isMobile)
    {
        if (isMobile) return 0;
        try
        {
            using var probe = Renderer.CreateOffscreen(RendererOptions.Default with { Width = 16, Height = 16, Shadows = false, Bloom = false });
            return probe.Capabilities.DeviceType == GpuDeviceType.DiscreteGpu ? 1 : 0;
        }
        catch (Exception) { return 1; }
    }

    public void SaveProfile()
    {
        try { Store.Save(Profile); }
        catch (IOException) { /* A full disk must not end the hunt; the next save retries. */ }
        catch (UnauthorizedAccessException) { }
    }

    public void SaveSettings()
    {
        try { Settings.Save(); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose()
    {
        Audio.Dispose();
        Pads?.Dispose();
    }
}
