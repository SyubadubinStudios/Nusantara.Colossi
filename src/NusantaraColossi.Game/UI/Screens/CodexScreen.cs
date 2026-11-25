using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NusantaraColossi.Core;

using Palette = NusantaraColossi.Game.UI.Theme;

namespace NusantaraColossi.Game.UI.Screens;

/// <summary>Inscription archive: each guardian laid to rest unseals its lore and prasasti.</summary>
public sealed class CodexScreen : Screen
{
    private readonly ContentControl detail = new();
    private readonly StackPanel list = new() { Spacing = 0 };

    public CodexScreen(Shell shell, string? select = null) : base(shell)
    {
        foreach (var guardian in Colossi.All)
        {
            var g = guardian;
            var known = Ctx.Profile.IsDefeated(g.Id);
            list.Children.Add(Palette.MenuItem(g.Name.Get(), () => Show(g), detail: known ? "◆" : null));
        }

        var left = new StackPanel { Width = Ctx.IsMobile ? 240 : 320, Margin = new Thickness(Ctx.IsMobile ? 24 : 56, 40, 24, 32) };
        left.Children.Add(Palette.Eyebrow(Loc.T("codex.eyebrow"), Palette.Nadi));
        left.Children.Add(Palette.Title(Loc.T("codex.title"), 38));
        left.Children.Add(Palette.Rule(Palette.Prada, 80).Also(r => r.Margin = new Thickness(0, 12, 0, 18)));
        left.Children.Add(list);
        left.Children.Add(Palette.Secondary("← " + Loc.T("menu.back"), () => Back()).Also(b => b.Margin = new Thickness(0, 24, 0, 0)));

        var layout = new DockPanel();
        DockPanel.SetDock(left, Dock.Left);
        layout.Children.Add(left);
        layout.Children.Add(new ScrollViewer { Content = detail, Padding = new Thickness(24, 40, 56, 40) });
        Content = new Border { Background = Palette.Scrim, Child = layout };
        Show(select is null ? Colossi.All.FirstOrDefault(c => Ctx.Profile.IsDefeated(c.Id)) ?? Colossi.All[0] : Colossi.Get(select));
    }

    private void Show(ColossusBlueprint g)
    {
        var known = Ctx.Profile.IsDefeated(g.Id);
        var region = Regions.ForColossus(g.Id);
        var s = new StackPanel { MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var art = Images.Load(AssetPaths.ColossusArt(g.Id), 560);
        if (art is not null)
        {
            head.Children.Add(new Border
            {
                BorderBrush = Palette.Prada,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4),
                Margin = new Thickness(0, 0, 28, 0),
                Child = new Image { Source = art, Width = Ctx.IsMobile ? 160 : 260, Height = Ctx.IsMobile ? 200 : 320, Stretch = Stretch.UniformToFill, Opacity = known ? 1 : .3 },
            });
        }
        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
        title.Children.Add(Palette.Mono($"{region.Place.Get().ToUpperInvariant()} · {g.Height:0} M · {g.Cores.Length} {(Loc.Language == "en" ? "CORES" : "INTI")}", 11, Palette.Nadi));
        title.Children.Add(new TextBlock { Text = g.Name.Get(), FontFamily = Palette.Inscription, FontSize = 40, Foreground = Palette.Kapur, TextWrapping = TextWrapping.Wrap });
        title.Children.Add(Palette.Text(g.Epithet.Get(), 16, Palette.Prada).Also(t => t.FontStyle = FontStyle.Italic));
        Grid.SetColumn(title, art is null ? 0 : 1);
        head.Children.Add(title);
        s.Children.Add(head);

        s.Children.Add(Palette.Rule(Palette.Hairline).Also(r => r.Margin = new Thickness(0, 24)));
        if (known)
        {
            s.Children.Add(Palette.Text(g.Lore.Get(), 15.5, Palette.Kapur).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.LineHeight = 25; }));
            s.Children.Add(new Border
            {
                BorderBrush = Palette.Prada,
                BorderThickness = new Thickness(2, 0, 0, 0),
                Padding = new Thickness(18, 6),
                Margin = new Thickness(0, 24, 0, 0),
                Child = new TextBlock { Text = g.Inscription.Get(), FontFamily = Palette.Inscription, FontSize = 18, Foreground = Palette.PradaBright, TextWrapping = TextWrapping.Wrap, LineHeight = 30 },
            });
        }
        else
        {
            s.Children.Add(Palette.Text(Loc.T("codex.sealed"), 15, Palette.KapurMuted).Also(t => t.TextWrapping = TextWrapping.Wrap));
        }
        s.Children.Add(Palette.Mono(Loc.T("codex.hint").ToUpperInvariant(), 10.5, Palette.Nadi).Also(t => t.Margin = new Thickness(0, 28, 0, 6)));
        s.Children.Add(Palette.Text(g.Hint.Get(), 14, Palette.KapurMuted).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.LineHeight = 22; }));
        detail.Content = s;
    }
}
