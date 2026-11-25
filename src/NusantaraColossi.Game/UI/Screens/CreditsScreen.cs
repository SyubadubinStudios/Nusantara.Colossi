using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using NusantaraColossi.Core;
using GamepadButton = ThreeNet.GamepadButton;

using Palette = NusantaraColossi.Game.UI.Theme;

namespace NusantaraColossi.Game.UI.Screens;

/// <summary>
/// Film-style end credits: a centred roll drifts up over the 3D stage, fades at both edges and
/// comes to rest on a gold kawung seal. Hold Enter, Space, A or a touch to speed it up.
/// </summary>
public sealed class CreditsScreen : Screen
{
    private const double Speed = 46;      // DIP per second
    private const double FastFactor = 7;

    private readonly StackPanel roll;
    private readonly TranslateTransform shift = new();
    private readonly Panel viewport;
    private readonly Control finale;
    private readonly Button back;
    private readonly TextBlock hint;
    private double offset = double.NaN;
    private bool fast, padFast, finished;

    public CreditsScreen(Shell shell) : base(shell)
    {
        var lang = Loc.Language;
        var en = lang == "en";

        roll = new StackPanel { Width = Ctx.IsMobile ? 520 : 640, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, RenderTransform = shift };

        void Add(Control c, double top = 0) { c.Margin = new Thickness(0, top, 0, 0); roll.Children.Add(c); }
        TextBlock Centered(TextBlock t) { t.HorizontalAlignment = HorizontalAlignment.Center; t.TextAlignment = TextAlignment.Center; t.TextWrapping = TextWrapping.Wrap; return t; }
        TextBlock Role(string text) => Centered(Palette.Mono(text.ToUpperInvariant(), 11, Palette.Nadi)).Also(t => t.LetterSpacing = 3);
        TextBlock Name(string text, double size = 24) => Centered(Palette.Title(text, size, Palette.Kapur));
        TextBlock Line(string text) => Centered(Palette.Text(text, 14.5, Palette.KapurMuted)).Also(t => t.LineHeight = 22);

        void Section(string role, params string[] names)
        {
            Add(Role(role), 72);
            Add(Palette.Rule(Palette.Prada, 28).Also(r => r.HorizontalAlignment = HorizontalAlignment.Center), 10);
            var first = true;
            foreach (var n in names) { Add(Name(n), first ? 14 : 6); first = false; }
        }

        void Notes(string role, params string[] lines)
        {
            Add(Role(role), 72);
            Add(Palette.Rule(Palette.Prada, 28).Also(r => r.HorizontalAlignment = HorizontalAlignment.Center), 10);
            var first = true;
            foreach (var l in lines) { Add(Line(l), first ? 14 : 8); first = false; }
        }

        // Opening card.
        Add(Centered(Palette.Eyebrow("GARUDA NET · 2045 · NUSANTARA", Palette.Nadi)));
        Add(Centered(Palette.Title("NUSANTARA", 54, Palette.Kapur)), 14);
        Add(Centered(Palette.Title("COLOSSI", 54, Palette.Prada)).Also(t => t.LetterSpacing = 18), -6);
        Add(Centered(new TextBlock { Text = Loc.T("credit"), FontFamily = Palette.Inscription, FontSize = 19, Foreground = Palette.PradaBright, LineHeight = 30 }), 22);

        Section(en ? "Created, directed & programmed by" : "Dibuat, diarahkan & diprogram oleh", "Ariana Mischa Fadhila");
        Section("Studio", "Syubadubin Studios");
        Section(en ? "Game design · World · Story" : "Desain game · Dunia · Cerita", "Ariana Mischa Fadhila");

        // The guardians take their bow.
        Add(Role(en ? "The guardians" : "Para Penjaga"), 88);
        Add(Palette.Rule(Palette.Prada, 28).Also(r => r.HorizontalAlignment = HorizontalAlignment.Center), 10);
        var shown = Ctx.Profile.VisibleRegions().Select(r => r.ColossusId).ToHashSet();
        foreach (var g in Colossi.All.Where(g => shown.Contains(g.Id)))
        {
            Add(Name(g.Name.Get(lang), 22), 22);
            Add(Centered(Palette.Text(g.Epithet.Get(lang), 13, Palette.KapurMuted)).Also(t => t.FontStyle = FontStyle.Italic), 2);
        }

        Section(en ? "Climber" : "Pemanjat", "Arka");
        Section(en ? "Drone companion" : "Drone pendamping", "GARUDA");

        Notes(en ? "Technology" : "Teknologi",
            ".NET 10 · C#",
            "Avalonia UI — " + (en ? "every screen, on desktop and Android" : "seluruh layar, di desktop dan Android"),
            "Three.Net (ThreeNet) — " + (en ? "rendering, audio, gamepads and UDP networking on a Rust wgpu core" : "rendering, audio, gamepad, dan jaringan UDP di atas inti Rust wgpu"));
        Notes(en ? "Art" : "Seni",
            en ? "2D concept art and portraits generated with Rodin GPT Image Flare, directed by the studio." : "Konsep 2D dan potret dibuat dengan Rodin GPT Image Flare, diarahkan oleh studio.",
            en ? "3D models via Rodin; sculpting, rigging and animation in Blender." : "Model 3D melalui Rodin; sculpting, rigging, dan animasi di Blender.");
        Notes(en ? "Music & sound" : "Musik & suara",
            en ? "Score and effects synthesised by AudioForge: gamelan voices (saron, bonang, gong ageng, kendang) tuned to pelog." : "Musik dan efek disintesis AudioForge: suara gamelan (saron, bonang, gong ageng, kendang) bertala pelog.",
            en ? "Voice of GARUDA: ElevenLabs." : "Suara GARUDA: ElevenLabs.");
        Notes(en ? "Typefaces (SIL Open Font License)" : "Huruf (SIL Open Font License)", "Marcellus SC · Hanken Grotesk · JetBrains Mono");
        Notes(en ? "Thanks" : "Terima kasih", en ? "To the stories of the archipelago, which were here first." : "Untuk kisah-kisah Nusantara, yang lebih dulu ada.");

        // Closing seal: rests at the centre of the screen.
        var seal = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        seal.Children.Add(Kawung(88).Also(k => k.HorizontalAlignment = HorizontalAlignment.Center));
        seal.Children.Add(Centered(Palette.Title(en ? "Thank you for climbing." : "Terima kasih telah memanjat.", 30, Palette.PradaBright)).Also(t => t.Margin = new Thickness(0, 22, 0, 0)));
        seal.Children.Add(Centered(Palette.Mono("SYUBADUBIN STUDIOS · 2026", 11, Palette.KapurMuted)).Also(t => { t.Margin = new Thickness(0, 14, 0, 0); t.LetterSpacing = 3; }));
        finale = seal;
        Add(seal, 160);

        viewport = new Panel
        {
            ClipToBounds = true,
            Children = { roll },
            OpacityMask = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Colors.Transparent, 0),
                    new GradientStop(Colors.Black, .16),
                    new GradientStop(Colors.Black, .84),
                    new GradientStop(Colors.Transparent, 1)
                }
            }
        };

        back = Palette.Secondary("← " + Loc.T("menu.back"), () => Back());
        hint = Palette.Mono(en ? "HOLD ENTER · SPACE · TOUCH TO SPEED UP" : "TAHAN ENTER · SPASI · SENTUH UNTUK MEMPERCEPAT", 10, Palette.KapurMuted);
        hint.LetterSpacing = 2;
        hint.VerticalAlignment = VerticalAlignment.Center;
        var bar = new DockPanel { Margin = new Thickness(Ctx.IsMobile ? 24 : 48, 0, Ctx.IsMobile ? 24 : 48, 28), VerticalAlignment = VerticalAlignment.Bottom, LastChildFill = false };
        DockPanel.SetDock(back, Dock.Left);
        DockPanel.SetDock(hint, Dock.Right);
        bar.Children.Add(back);
        bar.Children.Add(hint);

        var root = new Grid { Background = Palette.Scrim, Children = { viewport, bar } };
        root.PointerPressed += (_, e) => { if (e.Source is not Visual v || !back.IsVisualAncestorOf(v)) fast = true; };
        root.PointerReleased += (_, _) => fast = false;
        root.PointerCaptureLost += (_, _) => fast = false;
        Content = root;

        AddHandler(KeyDownEvent, (_, e) => { if (e.Key is Key.Enter or Key.Space) { fast = true; e.Handled = true; } }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, (_, e) => { if (e.Key is Key.Enter or Key.Space) fast = false; }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    /// <summary>Four gold ellipses round a cyan seed: the kawung motif used across the game's UI.</summary>
    private static Control Kawung(double size)
    {
        var c = new Canvas { Width = size, Height = size };
        var petal = size * .44;
        for (var i = 0; i < 4; i++)
        {
            var e = new Ellipse { Width = petal * .62, Height = petal, Stroke = Palette.Prada, StrokeThickness = 1.6, Fill = new SolidColorBrush(Color.FromArgb(0x30, 0xC8, 0xA0, 0x4A)) };
            Canvas.SetLeft(e, size / 2 - petal * .31);
            Canvas.SetTop(e, size / 2 - petal);
            e.RenderTransformOrigin = new RelativePoint(.5, 1, RelativeUnit.Relative);
            e.RenderTransform = new RotateTransform(45 + i * 90);
            c.Children.Add(e);
        }
        var seed = new Ellipse { Width = size * .1, Height = size * .1, Fill = Palette.Nadi };
        Canvas.SetLeft(seed, size * .45);
        Canvas.SetTop(seed, size * .45);
        c.Children.Add(seed);
        return c;
    }

    public override void Enter()
    {
        base.Enter();
        back.Focus();
    }

    public override void Tick(float dt)
    {
        base.Tick(dt);
        var height = viewport.Bounds.Height;
        if (height <= 0 || finale.Bounds.Height <= 0) return;
        if (double.IsNaN(offset)) offset = height * .82;
        var rest = height / 2 - (finale.Bounds.Y + finale.Bounds.Height / 2);
        if (!finished)
        {
            var pad = Ctx.Input.Pad;
            if (pad.IsConnected && pad.WasPressed(GamepadButton.South)) padFast = true;
            if (!pad.IsConnected || pad.WasReleased(GamepadButton.South)) padFast = false;
            offset -= Speed * (fast || padFast ? FastFactor : 1) * dt;
            if (offset <= rest)
            {
                offset = rest;
                finished = true;
                hint.IsVisible = false;
            }
        }
        shift.Y = Math.Round(offset);
    }
}
