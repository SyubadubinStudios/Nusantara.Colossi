using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NusantaraColossi.Core;
using NusantaraColossi.Game.UI.Controls;

using Palette = NusantaraColossi.Game.UI.Theme;

namespace NusantaraColossi.Game.UI.Screens;

/// <summary>Region select: a survey chart of the archipelago and a dossier for the chosen guardian.</summary>
public sealed class MapScreen : Screen
{
    private readonly ArchipelagoMap map;
    private readonly ContentControl dossier = new();
    private bool legend;

    public MapScreen(Shell shell, RegionId? select = null) : base(shell)
    {
        var profile = Ctx.Profile;
        map = new ArchipelagoMap
        {
            Profile = profile,
            Selected = select ?? (Regions.All.FirstOrDefault(r => profile.IsUnlocked(r.Id) && !profile.IsDefeated(r.ColossusId)) ?? Regions.All[0]).Id,
        };
        map.SelectionChanged += _ => BuildDossier();

        var header = new StackPanel { Margin = new Thickness(Ctx.IsMobile ? 24 : 56, 36, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
        header.Children.Add(Palette.Eyebrow(Loc.T("map.eyebrow"), Palette.Nadi));
        header.Children.Add(Palette.Title(Loc.T("map.title"), 40));

        var back = Palette.Secondary("← " + Loc.T("menu.back"), () => Back());
        back.Margin = new Thickness(Ctx.IsMobile ? 24 : 56, 0, 0, 32);
        back.VerticalAlignment = VerticalAlignment.Bottom;
        back.HorizontalAlignment = HorizontalAlignment.Left;

        var chart = new Grid { Children = { map, header, back } };
        var panel = new Border
        {
            Width = Ctx.IsMobile ? 340 : 420,
            Background = Palette.Scrim,
            BorderBrush = Palette.Hairline,
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = new ScrollViewer { Content = dossier, Padding = new Thickness(32, 36, 32, 32) },
        };
        var layout = new DockPanel();
        DockPanel.SetDock(panel, Dock.Right);
        layout.Children.Add(panel);
        layout.Children.Add(chart);

        Content = new Border { Background = new SolidColorBrush(Color.FromArgb(225, 16, 20, 18)), Child = layout };
        BuildDossier();
    }

    private void Cycle(int step)
    {
        var i = ((int)map.Selected + step + Regions.All.Length) % Regions.All.Length;
        map.Selected = (RegionId)i;
        BuildDossier();
    }

    private void BuildDossier()
    {
        var region = Regions.Get(map.Selected);
        var guardian = Colossi.Get(region.ColossusId);
        var profile = Ctx.Profile;
        var unlocked = profile.IsUnlocked(region.Id);
        profile.Hunts.TryGetValue(guardian.Id, out var record);

        var s = new StackPanel { Spacing = 0 };
        var nav = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var prev = Palette.Secondary("‹", () => Cycle(-1));
        var next = Palette.Secondary("›", () => Cycle(1));
        prev.Padding = next.Padding = new Thickness(12, 4);
        DockPanel.SetDock(prev, Dock.Left);
        DockPanel.SetDock(next, Dock.Right);
        nav.Children.Add(prev);
        nav.Children.Add(next);
        nav.Children.Add(Palette.Mono($"{(int)region.Id + 1:00} / 08 · {region.Place.Get().ToUpperInvariant()}", 11, Palette.Nadi).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; }));
        s.Children.Add(nav);

        var portrait = Images.Load(AssetPaths.ColossusArt(guardian.Id), 760);
        if (portrait is not null)
        {
            s.Children.Add(new Border
            {
                BorderBrush = Palette.Prada,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4),
                Margin = new Thickness(0, 6, 0, 18),
                Child = new Image { Source = portrait, Stretch = Stretch.UniformToFill, Height = Ctx.IsMobile ? 170 : 230, Opacity = unlocked ? 1 : .35 },
            });
        }

        s.Children.Add(new TextBlock { Text = guardian.Name.Get(), FontFamily = Palette.Inscription, FontSize = 32, Foreground = unlocked ? Palette.Kapur : Palette.KapurMuted, TextWrapping = TextWrapping.Wrap });
        s.Children.Add(Palette.Text(guardian.Epithet.Get(), 15, Palette.Prada).Also(t => t.FontStyle = FontStyle.Italic));
        s.Children.Add(Palette.Rule(Palette.Hairline).Also(r => r.Margin = new Thickness(0, 16)));
        s.Children.Add(Palette.Text(region.Blurb.Get(), 14.5, Palette.KapurMuted).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.LineHeight = 22; }));

        var facts = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto"), Margin = new Thickness(0, 18, 0, 0) };
        void Fact(int row, string label, string value, IBrush? brush = null)
        {
            var l = Palette.Mono(label.ToUpperInvariant(), 10.5, Palette.KapurMuted);
            l.Margin = new Thickness(0, 4, 16, 4);
            Grid.SetRow(l, row);
            var v = Palette.Text(value, 13.5, brush ?? Palette.Kapur);
            v.TextWrapping = TextWrapping.Wrap;
            v.Margin = new Thickness(0, 3);
            Grid.SetRow(v, row);
            Grid.SetColumn(v, 1);
            facts.Children.Add(l);
            facts.Children.Add(v);
        }
        Fact(0, Loc.T("map.hazard"), Loc.T("hazard." + region.Hazard), region.Hazard == Hazard.None ? Palette.KapurMuted : Palette.Bara);
        Fact(1, Loc.Language == "en" ? "Height" : "Tinggi", $"{guardian.Height:0} m · {guardian.Cores.Length} {(Loc.Language == "en" ? "cores" : "inti")}");
        Fact(2, Loc.Language == "en" ? "Record" : "Rekor", record is { Victories: > 0 } ? Loc.F("map.best", FormatTime(record.BestSeconds), record.BestRank) : Loc.T("map.unhunted"), record is { Victories: > 0 } ? Palette.Prada : Palette.KapurMuted);
        s.Children.Add(facts);

        if (unlocked)
        {
            var legendBox = new CheckBox { IsChecked = legend, Margin = new Thickness(0, 20, 0, 0), Content = Palette.Text(Loc.T("map.legend"), 14, Palette.Kapur) };
            legendBox.IsCheckedChanged += (_, _) => legend = legendBox.IsChecked == true;
            s.Children.Add(legendBox);
            s.Children.Add(Palette.Text(Loc.T("map.legendHint"), 12.5, Palette.KapurMuted).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(28, 0, 0, 0); }));
            var hunt = Palette.Primary(Loc.T("map.hunt"), () => StartHunt(region, guardian));
            hunt.Margin = new Thickness(0, 26, 0, 0);
            hunt.HorizontalAlignment = HorizontalAlignment.Stretch;
            hunt.HorizontalContentAlignment = HorizontalAlignment.Center;
            s.Children.Add(hunt);
        }
        else
        {
            s.Children.Add(Palette.Text(Loc.T("map.locked"), 13.5, Palette.Bara).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(0, 22, 0, 0); }));
        }
        dossier.Content = s;
    }

    private void StartHunt(RegionDefinition region, ColossusBlueprint guardian)
    {
        var options = new EncounterOptions { Legend = legend, Abilities = Ctx.Profile.Abilities, Seed = Environment.TickCount };
        Shell.FadeThrough(() => Shell.Navigate(HuntScreen.Solo(Shell, region, guardian, options)));
    }

    public static string FormatTime(float seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";

    public override void Tick(float dt)
    {
        base.Tick(dt);
        map.Tick(dt);
    }
}

internal static class FluentExtensions
{
    /// <summary>Inline configuration for code-built UI.</summary>
    public static T Also<T>(this T value, Action<T> configure)
    {
        configure(value);
        return value;
    }
}
