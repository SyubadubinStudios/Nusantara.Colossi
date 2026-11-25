using Avalonia.Threading;
using NusantaraColossi.Core;
using NusantaraColossi.Game.UI.Screens;

namespace NusantaraColossi.Game.UI;

/// <summary>
/// `--screenshot &lt;folder&gt;`: walks through the main screens, letting each settle, and saves
/// PNGs for the README and docs. Hunts run on autopilot so Arka is seen approaching the guardian.
/// </summary>
public static class ScreenshotTour
{
    public static void Start(Shell shell, string folder, Action done)
    {
        var language = Environment.GetEnvironmentVariable("NC_SHOT_LANG");
        if (language is "en" or "id") Loc.Language = language;
        // NC_SHOT_SETTINGS=1: accessibility tour (settings, key capture, high-contrast HUD at 130%).
        if (Environment.GetEnvironmentVariable("NC_SHOT_SETTINGS") == "1")
        {
            var st = shell.Context.Settings;
            var (hc0, sc0) = (st.HighContrastHud, st.UiScale);
            SettingsScreen? scr = null;
            void Set(bool hc, float sc) { st.HighContrastHud = hc; st.UiScale = sc; Theme.ApplyAccessibility(st); }
            Run(
            [
                (6.0, () => { Set(false, 1f); shell.Navigate(scr = new SettingsScreen(shell)); }),
                (1.5, () => shell.Capture(Path.Combine(folder, "a11y-settings.png"))),
                (.1, () => typeof(SettingsScreen).GetMethod("BeginCapture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(scr, [GameAction.Jump, false])),
                (.5, () => shell.Capture(Path.Combine(folder, "a11y-capture.png"))),
                (.1, () => { scr!.Back(); Set(true, 1.3f); shell.Navigate(new SettingsScreen(shell)); }),
                (1.5, () => shell.Capture(Path.Combine(folder, "a11y-settings-hc130.png"))),
                (.1, () => { Set(true, 1f); Hunt(shell, RegionId.Bromo, 30); }),
                (9.0, () => shell.Capture(Path.Combine(folder, "a11y-hunt-hc.png"))),
                (.1, () => { Set(false, 1f); Hunt(shell, RegionId.Bromo, 30); }),
                (9.0, () => shell.Capture(Path.Combine(folder, "a11y-hunt.png"))),
                (.1, () => { Set(hc0, sc0); shell.Context.SaveSettings(); }),
                (.5, done),
            ], 0);
            return;
        }
        // NC_SHOT_REGIONS=Flores,Banda: only capture hunts in these regions (asset checks).
        if (Environment.GetEnvironmentVariable("NC_SHOT_REGIONS") is { Length: > 0 } only)
        {
            var hunts = new List<(double Delay, Action Action)> { (6.0, () => { }) };
            foreach (var name in only.Split(','))
            {
                if (!Enum.TryParse<RegionId>(name.Trim(), true, out var id)) continue;
                hunts.Add((.1, () => Hunt(shell, id, 30)));
                hunts.Add((12.0, () => shell.Capture(Path.Combine(folder, $"hunt-{id.ToString().ToLowerInvariant()}.png"))));
            }
            hunts.Add((.5, done));
            Run(hunts, 0);
            return;
        }
        var suffix = Loc.Language == "en" ? "" : "-id";
        var steps = new List<(double Delay, Action Action)>
        {
            (9.0, () => shell.Capture(Path.Combine(folder, $"title{suffix}.png"))),
            (.1, () => shell.Navigate(new MapScreen(shell, RegionId.Bromo))),
            (2.0, () => shell.Capture(Path.Combine(folder, $"map{suffix}.png"))),
            (.1, () => shell.Navigate(new CodexScreen(shell, "kerbau-langit"))),
            (1.5, () => shell.Capture(Path.Combine(folder, $"codex{suffix}.png"))),
            (.1, () => shell.Navigate(new SkillsScreen(shell))),
            (1.5, () => shell.Capture(Path.Combine(folder, $"skills{suffix}.png"))),
            (.1, () => Hunt(shell, RegionId.Bromo, 30)),
            (9.0, () => shell.Capture(Path.Combine(folder, $"hunt-bromo{suffix}.png"))),
            (.1, () => Hunt(shell, RegionId.Jakarta, 30)),
            (9.5, () => shell.Capture(Path.Combine(folder, $"hunt-jakarta{suffix}.png"))),
            (.1, () => Hunt(shell, RegionId.RajaAmpat, 30)),
            (9.5, () => shell.Capture(Path.Combine(folder, $"hunt-rajaampat{suffix}.png"))),
            (.1, () => Hunt(shell, RegionId.Ijen, 30)),
            (9.5, () => shell.Capture(Path.Combine(folder, $"hunt-ijen{suffix}.png"))),
            (.5, done),
        };
        Run(steps, 0);
    }

    private static void Hunt(Shell shell, RegionId id, float autopilot)
    {
        var region = Regions.Get(id);
        var screen = HuntScreen.Solo(shell, region, Colossi.Get(region.ColossusId), new EncounterOptions { Seed = 3 });
        shell.Navigate(screen);
        screen.Autopilot(autopilot);
    }

    private static void Run(List<(double Delay, Action Action)> steps, int index)
    {
        if (index >= steps.Count) return;
        DispatcherTimer.RunOnce(() =>
        {
            try { steps[index].Action(); }
            catch (Exception ex) { Console.Error.WriteLine($"screenshot step {index}: {ex.Message}"); }
            Run(steps, index + 1);
        }, TimeSpan.FromSeconds(steps[index].Delay));
    }
}
