using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NusantaraColossi.Core;

using Palette = NusantaraColossi.Game.UI.Theme;

namespace NusantaraColossi.Game.UI.Screens;

/// <summary>Three skill trees, three tiers each. A tier opens once the tier below it is learned.</summary>
public sealed class SkillsScreen : Screen
{
    public SkillsScreen(Shell shell) : base(shell) => Build();

    private void Build()
    {
        var profile = Ctx.Profile;
        var root = new StackPanel { Margin = new Thickness(Ctx.IsMobile ? 24 : 56, 40, Ctx.IsMobile ? 24 : 56, 32) };
        var header = new DockPanel();
        var titles = new StackPanel();
        titles.Children.Add(Palette.Eyebrow(Loc.T("skills.help"), Palette.Nadi));
        titles.Children.Add(Palette.Title(Loc.T("skills.title"), 38));
        var points = new TextBlock { Text = Loc.F("skills.points", profile.SkillPoints), FontFamily = Palette.Inscription, FontSize = 22, Foreground = profile.SkillPoints > 0 ? Palette.PradaBright : Palette.KapurMuted, VerticalAlignment = VerticalAlignment.Bottom };
        DockPanel.SetDock(points, Dock.Right);
        header.Children.Add(points);
        header.Children.Add(titles);
        root.Children.Add(header);
        root.Children.Add(Palette.Rule(Palette.Prada, 80).Also(r => { r.Margin = new Thickness(0, 12, 0, 24); r.HorizontalAlignment = HorizontalAlignment.Left; }));

        var trees = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };
        foreach (var tree in Enum.GetValues<SkillTree>())
        {
            var column = new StackPanel { Spacing = 12, Margin = new Thickness(0, 0, 20, 0) };
            column.Children.Add(Palette.Mono(Loc.T("skills.tree." + tree).ToUpperInvariant(), 12, Palette.Nadi));
            foreach (var skill in Skills.All.Where(s => s.Tree == tree).OrderBy(s => s.Tier))
                column.Children.Add(Card(skill, profile));
            Grid.SetColumn(column, (int)tree);
            trees.Children.Add(column);
        }
        root.Children.Add(trees);
        root.Children.Add(Palette.Secondary("← " + Loc.T("menu.back"), () => Back()).Also(b => b.Margin = new Thickness(0, 28, 0, 0)));
        Content = new Border { Background = Palette.Scrim, Child = new ScrollViewer { Content = root } };
    }

    private Control Card(SkillDefinition skill, Profile profile)
    {
        var owned = profile.Skills.Contains(skill.Id);
        var canBuy = profile.CanBuy(skill);
        var s = new StackPanel { Spacing = 6 };
        var head = new DockPanel();
        var tier = Palette.Mono(new string('◆', skill.Tier), 11, owned ? Palette.Prada : Palette.KapurMuted);
        DockPanel.SetDock(tier, Dock.Right);
        head.Children.Add(tier);
        head.Children.Add(new TextBlock { Text = skill.Name.Get(), FontFamily = Palette.Inscription, FontSize = 18, Foreground = owned ? Palette.PradaBright : Palette.Kapur, TextWrapping = TextWrapping.Wrap });
        s.Children.Add(head);
        s.Children.Add(Palette.Text(skill.Description.Get(), 13.5, Palette.KapurMuted).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.LineHeight = 20; }));
        if (owned) s.Children.Add(Palette.Mono(Loc.T("skills.owned").ToUpperInvariant(), 10.5, Palette.Prada));
        else
        {
            var learn = Palette.Secondary(Loc.F("skills.learn", skill.Cost), () =>
            {
                if (!profile.Buy(skill.Id)) return;
                Ctx.SaveProfile();
                Build();
            });
            learn.IsEnabled = canBuy;
            learn.HorizontalAlignment = HorizontalAlignment.Left;
            s.Children.Add(learn);
        }
        return new Border
        {
            Background = owned ? Palette.AndesiteRaised : Brushes.Transparent,
            BorderBrush = owned ? Palette.Prada : Palette.Hairline,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 14),
            Child = s,
        };
    }
}
