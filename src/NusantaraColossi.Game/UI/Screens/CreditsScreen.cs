using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using Palette = NusantaraColossi.Game.UI.Theme;

namespace NusantaraColossi.Game.UI.Screens;

public sealed class CreditsScreen : Screen
{
    public CreditsScreen(Shell shell) : base(shell)
    {
        var en = Loc.Language == "en";
        var s = new StackPanel { Margin = new Thickness(Ctx.IsMobile ? 24 : 96, 48, 24, 40), MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
        s.Children.Add(Palette.Eyebrow("NUSANTARA COLOSSI", Palette.Nadi));
        s.Children.Add(Palette.Title(Loc.T("credits.title"), 38));
        s.Children.Add(Palette.Rule(Palette.Prada, 80).Also(r => { r.Margin = new Thickness(0, 12, 0, 24); r.HorizontalAlignment = HorizontalAlignment.Left; }));
        s.Children.Add(new TextBlock { Text = Loc.T("credit"), FontFamily = Palette.Inscription, FontSize = 24, Foreground = Palette.PradaBright, TextWrapping = TextWrapping.Wrap, LineHeight = 36 });

        void Section(string title, params string[] lines)
        {
            s.Children.Add(Palette.Mono(title.ToUpperInvariant(), 11, Palette.Nadi).Also(t => t.Margin = new Thickness(0, 26, 0, 6)));
            foreach (var line in lines) s.Children.Add(Palette.Text(line, 14.5, Palette.Kapur).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.LineHeight = 22; }));
        }

        Section(en ? "Design, direction & code" : "Desain, arahan & kode", "Ariana Mischa Fadhila — Syubadubin Studios");
        Section(en ? "Technology" : "Teknologi",
            ".NET 10 · C#",
            "Avalonia UI — " + (en ? "every screen, on desktop and Android" : "seluruh layar, di desktop dan Android"),
            "Three.Net (ThreeNet) — " + (en ? "rendering, audio, gamepads and UDP networking on a Rust wgpu core" : "rendering, audio, gamepad, dan jaringan UDP di atas inti Rust wgpu"));
        Section(en ? "Art & sound" : "Seni & suara",
            en ? "2D concept art and portraits generated with Rodin GPT Image Flare, directed by the studio." : "Konsep 2D dan potret dibuat dengan Rodin GPT Image Flare, diarahkan oleh studio.",
            en ? "3D models via Rodin; sculpting, rigging and animation in Blender." : "Model 3D melalui Rodin; sculpting, rigging, dan animasi di Blender.",
            en ? "Score and effects synthesised by AudioForge: gamelan voices (saron, bonang, gong ageng, kendang) tuned to pelog." : "Musik dan efek disintesis AudioForge: suara gamelan (saron, bonang, gong ageng, kendang) bertala pelog.");
        Section(en ? "Typefaces (SIL Open Font License)" : "Huruf (SIL Open Font License)", "Marcellus SC · Hanken Grotesk · JetBrains Mono");
        Section(en ? "Thanks" : "Terima kasih", en ? "To the stories of the archipelago, which were here first." : "Untuk kisah-kisah Nusantara, yang lebih dulu ada.");
        s.Children.Add(Palette.Secondary("← " + Loc.T("menu.back"), () => Back()).Also(b => { b.Margin = new Thickness(0, 32, 0, 0); b.HorizontalAlignment = HorizontalAlignment.Left; }));
        Content = new Border { Background = Palette.Scrim, Child = new ScrollViewer { Content = s } };
    }
}
