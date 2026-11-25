using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using NusantaraColossi.Core;

using Palette = NusantaraColossi.Game.UI.Theme;

namespace NusantaraColossi.Game.UI.Screens;

/// <summary>
/// Title: the next unbeaten guardian stands in the live 3D scene; the menu is a carved list on a
/// left-hand scrim, like text cut into a temple's foundation stone.
/// </summary>
public sealed class TitleScreen : Screen
{
    public TitleScreen(Shell shell) : base(shell)
    {
        var profile = Ctx.Profile;
        var started = profile.Hunts.Count > 0;

        var menu = new StackPanel { Spacing = 2, Margin = new Thickness(0, 26, 0, 0) };
        menu.Children.Add(Palette.MenuItem(Loc.T(started ? "menu.continue" : "menu.begin"), () => Shell.Navigate(new MapScreen(Shell))));
        menu.Children.Add(Palette.MenuItem(Loc.T("menu.coop"), () => Shell.Navigate(new CoopScreen(Shell))));
        menu.Children.Add(Palette.MenuItem(Loc.T("menu.codex"), () => Shell.Navigate(new CodexScreen(Shell)), detail: $"{profile.DefeatedCount}/8"));
        menu.Children.Add(Palette.MenuItem(Loc.T("menu.skills"), () => Shell.Navigate(new SkillsScreen(Shell)), detail: profile.SkillPoints > 0 ? Loc.F("skills.points", profile.SkillPoints) : null));
        menu.Children.Add(Palette.MenuItem(Loc.T("menu.settings"), () => Shell.Navigate(new SettingsScreen(Shell))));
        menu.Children.Add(Palette.MenuItem(Loc.T("menu.credits"), () => Shell.Navigate(new CreditsScreen(Shell))));
        if (!Ctx.IsMobile)
            menu.Children.Add(Palette.MenuItem(Loc.T("menu.quit"), () =>
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d) d.Shutdown();
            }));

        var title = new StackPanel { Spacing = 0 };
        title.Children.Add(Palette.Eyebrow("GARUDA NET · 2045 · NUSANTARA", Palette.Nadi));
        title.Children.Add(new TextBlock { Text = "NUSANTARA", FontFamily = Palette.Inscription, FontSize = 78, Foreground = Palette.Kapur, LetterSpacing = 6, Margin = new Thickness(-3, 18, 0, -18) });
        title.Children.Add(new TextBlock { Text = "COLOSSI", FontFamily = Palette.Inscription, FontSize = 78, Foreground = Palette.Prada, LetterSpacing = 22, Margin = new Thickness(-3, 0, 0, 0) });
        title.Children.Add(Palette.Rule(Palette.Prada, 96));
        title.Children.Add(new TextBlock
        {
            Text = Loc.T("tagline"),
            FontFamily = Palette.Body,
            FontSize = 16,
            LineHeight = 25,
            Foreground = Palette.KapurMuted,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 430,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 16, 0, 0),
        });

        var column = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 40) };
        column.Children.Add(title);
        column.Children.Add(menu);

        // A gold engraving line runs the full height beside the text: the foundation-stone edge.
        var engraving = new Border { Width = 1, Background = Palette.Prada, Opacity = .55, Margin = new Thickness(0, 64, 30, 64) };
        var left = new DockPanel { Margin = new Thickness(Ctx.IsMobile ? 28 : 72, 0, 0, 0) };
        DockPanel.SetDock(engraving, Dock.Left);
        left.Children.Add(engraving);
        left.Children.Add(column);

        var scrim = new Border
        {
            Width = 760,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsHitTestVisible = false,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.FromArgb(235, 16, 20, 18), 0), new GradientStop(Color.FromArgb(170, 16, 20, 18), .55), new GradientStop(Color.FromArgb(0, 16, 20, 18), 1) },
            },
        };

        var credit = new TextBlock
        {
            Text = Loc.T("credit"),
            FontFamily = Palette.Body,
            FontSize = 12.5,
            Foreground = Palette.KapurMuted,
            Margin = new Thickness(Ctx.IsMobile ? 28 : 103, 0, 0, 26),
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var status = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 34, 26), Spacing = 4 };
        var next = Regions.All.FirstOrDefault(r => profile.IsUnlocked(r.Id) && !profile.IsDefeated(r.ColossusId)) ?? Regions.All[0];
        var guardian = Colossi.Get(next.ColossusId);
        status.Children.Add(Palette.Mono($"GARUDA//SIGNAL  {profile.DefeatedCount}/8 {(Loc.Language == "en" ? "AT REST" : "TENANG")}", 11, Palette.Nadi));
        status.Children.Add(new TextBlock { Text = guardian.Name.Get(), FontFamily = Palette.Inscription, FontSize = 22, Foreground = Palette.Kapur, HorizontalAlignment = HorizontalAlignment.Right });
        status.Children.Add(new TextBlock { Text = $"{guardian.Epithet.Get()} · {next.Place.Get()}", FontFamily = Palette.Body, FontSize = 13, Foreground = Palette.KapurMuted, HorizontalAlignment = HorizontalAlignment.Right });

        var language = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 22, 28, 0), Spacing = 6 };
        foreach (var (code, label) in new[] { ("id", "ID"), ("en", "EN") })
        {
            var b = Palette.Secondary(label, () =>
            {
                Ctx.Settings.Language = Loc.Language = code;
                Ctx.SaveSettings();
                Shell.Navigate(new TitleScreen(Shell));
            });
            if (Loc.Language == code) b.BorderBrush = Palette.Prada;
            b.Padding = new Thickness(12, 6);
            language.Children.Add(b);
        }

        Content = new Grid { Children = { scrim, left, credit, status, language } };
        ShowcaseId = next.ColossusId;
    }

    private string ShowcaseId { get; }

    public override void Enter()
    {
        Ctx.Audio.StartMenu();
        Shell.Stage.ShowShowcase(ShowcaseId, Ctx.Settings.Quality);
    }

    public override bool Back() => false;
}
