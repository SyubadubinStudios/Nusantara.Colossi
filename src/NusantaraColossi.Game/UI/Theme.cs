using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

namespace NusantaraColossi.Game.UI;

/// <summary>
/// Design tokens. The palette carries meaning, not decoration:
/// gold (prada) = ancient and mythic (colossi, cores, inscriptions); cyan (nadi) = GARUDA technology;
/// ember (bara) = danger. Everything else is andesite stone, moss and limestone.
/// </summary>
public static class Theme
{
    public static readonly Color AndesiteColor = Color.Parse("#161B19");
    public static readonly Color AndesiteRaisedColor = Color.Parse("#1F2623");
    public static readonly Color MossColor = Color.Parse("#33493B");
    public static readonly Color PradaColor = Color.Parse("#C9A24B");
    public static readonly Color PradaBrightColor = Color.Parse("#EBC872");
    public static readonly Color NadiColor = Color.Parse("#3FD6CF");
    public static readonly Color BaraColor = Color.Parse("#E0603A");
    public static readonly Color KapurColor = Color.Parse("#E9E2CF");
    public static readonly Color KapurMutedColor = Color.Parse("#A39D8B");
    public static readonly Color HairlineColor = Color.Parse("#3A433E");

    private static readonly Color ScrimNormal = Color.FromArgb(0xD8, 0x10, 0x14, 0x12);
    private static readonly Color ScrimLightNormal = Color.FromArgb(0x9A, 0x10, 0x14, 0x12);

    public static readonly IBrush Andesite = new ImmutableSolidColorBrush(AndesiteColor);
    public static readonly IBrush AndesiteRaised = new ImmutableSolidColorBrush(AndesiteRaisedColor);
    public static readonly IBrush Moss = new ImmutableSolidColorBrush(MossColor);
    public static readonly IBrush Prada = new ImmutableSolidColorBrush(PradaColor);
    public static readonly IBrush PradaBright = new ImmutableSolidColorBrush(PradaBrightColor);
    public static readonly IBrush Nadi = new ImmutableSolidColorBrush(NadiColor);
    public static readonly IBrush Kapur = new ImmutableSolidColorBrush(KapurColor);

    // Brushes the high-contrast HUD setting retunes in place (mutable, so every control using them
    // updates live). Contrast on andesite #161B19 — normal → high contrast:
    //   KapurMuted 6.4:1 → 11.2:1, Hairline 1.7:1 → 4.8:1 (WCAG 1.4.11 needs 3:1 for UI outlines),
    //   Bara 4.9:1 → 7.5:1, and the scrims go more opaque so text over a bright sky stays ≥ 4.5:1.
    public static readonly IBrush Scrim = new SolidColorBrush(ScrimNormal);
    public static readonly IBrush ScrimLight = new SolidColorBrush(ScrimLightNormal);
    public static readonly IBrush Bara = new SolidColorBrush(BaraColor);
    public static readonly IBrush KapurMuted = new SolidColorBrush(KapurMutedColor);
    public static readonly IBrush Hairline = new SolidColorBrush(HairlineColor);

    public static readonly Color BaraHighContrastColor = Color.Parse("#FF8A5C");
    public static readonly Color KapurMutedHighContrastColor = Color.Parse("#D6CFBC");
    public static readonly Color HairlineHighContrastColor = Color.Parse("#7C8981");

    /// <summary>High-contrast HUD is on (see <see cref="ApplyAccessibility"/>).</summary>
    public static bool HighContrast { get; private set; }
    /// <summary>Danger colour for custom-drawn HUD, honouring high contrast.</summary>
    public static Color Danger => HighContrast ? BaraHighContrastColor : BaraColor;
    /// <summary>Inactive outline colour for custom-drawn HUD, honouring high contrast.</summary>
    public static Color Outline => HighContrast ? HairlineHighContrastColor : HairlineColor;

    /// <summary>Whole-UI scale (0.85–1.3), applied to every <see cref="Screen"/> through a layout transform.</summary>
    public static double UiScale { get; private set; } = 1;
    private static event Action? UiScaleChanged;

    /// <summary>Applies the accessibility settings: high-contrast palette and UI scale. Safe to call repeatedly.</summary>
    public static void ApplyAccessibility(GameSettings settings)
    {
        HighContrast = settings.HighContrastHud;
        ((SolidColorBrush)Scrim).Color = HighContrast ? Color.FromArgb(0xF0, 0x10, 0x14, 0x12) : ScrimNormal;
        ((SolidColorBrush)ScrimLight).Color = HighContrast ? Color.FromArgb(0xD0, 0x10, 0x14, 0x12) : ScrimLightNormal;
        ((SolidColorBrush)Bara).Color = HighContrast ? BaraHighContrastColor : BaraColor;
        ((SolidColorBrush)KapurMuted).Color = HighContrast ? KapurMutedHighContrastColor : KapurMutedColor;
        ((SolidColorBrush)Hairline).Color = HighContrast ? HairlineHighContrastColor : HairlineColor;
        var scale = Math.Clamp(settings.UiScale, .85, 1.3);
        if (Math.Abs(scale - UiScale) > 1e-4)
        {
            UiScale = scale;
            UiScaleChanged?.Invoke();
        }
    }

    /// <summary>Inscription face: carved Roman capitals, like a prasasti. Used sparingly, for names and titles.</summary>
    public static readonly FontFamily Inscription = new("avares://NusantaraColossi.Game/Assets/Fonts#Marcellus SC");
    public static readonly FontFamily Body = new("avares://NusantaraColossi.Game/Assets/Fonts#Hanken Grotesk");
    /// <summary>GARUDA telemetry only.</summary>
    public static readonly FontFamily Telemetry = new("avares://NusantaraColossi.Game/Assets/Fonts#JetBrains Mono");

    public static TextBlock Title(string text, double size, IBrush? brush = null) => new()
    {
        Text = text,
        FontFamily = Inscription,
        FontSize = size,
        Foreground = brush ?? Prada,
        LetterSpacing = size * .06,
        TextWrapping = TextWrapping.Wrap
    };

    public static TextBlock Text(string text, double size = 15, IBrush? brush = null, FontWeight weight = FontWeight.Normal) => new()
    {
        Text = text,
        FontFamily = Body,
        FontSize = size,
        FontWeight = weight,
        Foreground = brush ?? Kapur,
        TextWrapping = TextWrapping.Wrap,
        LineHeight = size * 1.45
    };

    /// <summary>Small uppercase label above a heading: says what kind of thing follows.</summary>
    public static TextBlock Eyebrow(string text, IBrush? brush = null) => new()
    {
        Text = text.ToUpperInvariant(),
        FontFamily = Body,
        FontSize = 11,
        FontWeight = FontWeight.SemiBold,
        LetterSpacing = 2.4,
        Foreground = brush ?? KapurMuted
    };

    public static TextBlock Mono(string text, double size = 12, IBrush? brush = null) => new()
    {
        Text = text,
        FontFamily = Telemetry,
        FontSize = size,
        Foreground = brush ?? Nadi,
        TextWrapping = TextWrapping.Wrap
    };

    public static Border Rule(IBrush? brush = null, double width = double.NaN) => new()
    {
        Height = 1,
        Width = width,
        Background = brush ?? Hairline,
        HorizontalAlignment = double.IsNaN(width) ? HorizontalAlignment.Stretch : HorizontalAlignment.Left
    };

    /// <summary>
    /// Menu action: plain text with a gold rule that grows in on hover/focus. No pill buttons —
    /// menus read like a carved list of chapters.
    /// </summary>
    public static Button MenuItem(string text, Action onClick, bool enabled = true, string? detail = null)
    {
        var rule = new Border { Width = 0, Height = 2, Background = Prada, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        var label = new TextBlock { Text = text, FontFamily = Body, FontSize = 19, FontWeight = FontWeight.Medium, Foreground = enabled ? Kapur : KapurMuted, VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(rule);
        row.Children.Add(label);
        if (detail is not null)
            row.Children.Add(new TextBlock { Text = detail, FontFamily = Body, FontSize = 13, Foreground = KapurMuted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 2, 0, 0) });
        var button = new Button
        {
            Content = row,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 7),
            IsEnabled = enabled,
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
        button.Classes.Add("menu");
        void Highlight(bool on)
        {
            rule.Width = on ? 28 : 0;
            label.Foreground = on ? PradaBright : enabled ? Kapur : KapurMuted;
        }
        button.PointerEntered += (_, _) => { Highlight(true); Sound?.Invoke("ui-hover"); };
        button.PointerExited += (_, _) => Highlight(button.IsFocused);
        button.GotFocus += (_, _) => Highlight(true);
        button.LostFocus += (_, _) => Highlight(false);
        button.Click += (_, _) => { Sound?.Invoke("ui-click"); onClick(); };
        return button;
    }

    /// <summary>Primary call to action: gold-outlined, inscription caps.</summary>
    public static Button Primary(string text, Action onClick, bool enabled = true)
    {
        var button = new Button
        {
            Content = new TextBlock { Text = text.ToUpperInvariant(), FontFamily = Inscription, FontSize = 17, LetterSpacing = 2, Foreground = enabled ? Andesite : KapurMuted },
            Background = enabled ? Prada : AndesiteRaised,
            BorderBrush = Prada,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(26, 12),
            IsEnabled = enabled,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
        button.Classes.Add("primary");
        button.Click += (_, _) => { Sound?.Invoke("ui-confirm"); onClick(); };
        return button;
    }

    public static Button Secondary(string text, Action onClick)
    {
        var button = new Button
        {
            Content = new TextBlock { Text = text, FontFamily = Body, FontSize = 14, FontWeight = FontWeight.Medium, Foreground = Kapur },
            Background = Brushes.Transparent,
            BorderBrush = Hairline,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(18, 10),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
        button.Classes.Add("secondary");
        button.Click += (_, _) => { Sound?.Invoke("ui-click"); onClick(); };
        return button;
    }

    private static Control ScaledScreenTemplate(Screen screen, INameScope scope)
    {
        var presenter = new Avalonia.Controls.Presenters.ContentPresenter
        {
            Name = "PART_ContentPresenter",
            [!Avalonia.Controls.Presenters.ContentPresenter.ContentProperty] = screen[!ContentControl.ContentProperty],
            [!Avalonia.Controls.Presenters.ContentPresenter.BackgroundProperty] = screen[!Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty],
        };
        scope.Register(presenter.Name!, presenter);
        var host = new LayoutTransformControl { Child = presenter, LayoutTransform = new ScaleTransform(UiScale, UiScale) };
        void Update() => host.LayoutTransform = new ScaleTransform(UiScale, UiScale);
        host.AttachedToVisualTree += (_, _) => { Update(); UiScaleChanged += Update; };
        host.DetachedFromVisualTree += (_, _) => UiScaleChanged -= Update;
        return host;
    }

    /// <summary>Hook for UI sounds; set by the audio layer.</summary>
    public static Action<string>? Sound { get; set; }

    public static Avalonia.Styling.Styles Styles()
    {
        var styles = new Avalonia.Styling.Styles();
        // UI scale: every screen's content sits in a LayoutTransformControl, so layout reflows at the
        // scaled size instead of overflowing (a RenderTransform would not).
        styles.Add(new Avalonia.Styling.Style(x => x.Is<Screen>())
        {
            Setters = { new Avalonia.Styling.Setter(Avalonia.Controls.Primitives.TemplatedControl.TemplateProperty, new Avalonia.Controls.Templates.FuncControlTemplate<Screen>(ScaledScreenTemplate)) }
        });
        // Visible keyboard focus everywhere, in gold.
        styles.Add(new Avalonia.Styling.Style(x => x.OfType<Button>().Class(":focus-visible"))
        {
            Setters = { new Avalonia.Styling.Setter(Button.BorderBrushProperty, PradaBright), new Avalonia.Styling.Setter(Button.BorderThicknessProperty, new Thickness(1)) }
        });
        styles.Add(new Avalonia.Styling.Style(x => x.OfType<Button>().Class("menu").Class(":pointerover").Template().OfType<Avalonia.Controls.Presenters.ContentPresenter>())
        {
            Setters = { new Avalonia.Styling.Setter(Avalonia.Controls.Presenters.ContentPresenter.BackgroundProperty, Brushes.Transparent) }
        });
        styles.Add(new Avalonia.Styling.Style(x => x.OfType<Button>().Class("menu").Class(":pressed").Template().OfType<Avalonia.Controls.Presenters.ContentPresenter>())
        {
            Setters = { new Avalonia.Styling.Setter(Avalonia.Controls.Presenters.ContentPresenter.BackgroundProperty, Brushes.Transparent) }
        });
        styles.Add(new Avalonia.Styling.Style(x => x.OfType<Button>().Class("primary").Class(":pointerover").Template().OfType<Avalonia.Controls.Presenters.ContentPresenter>())
        {
            Setters = { new Avalonia.Styling.Setter(Avalonia.Controls.Presenters.ContentPresenter.BackgroundProperty, PradaBright) }
        });
        styles.Add(new Avalonia.Styling.Style(x => x.OfType<Button>().Class("secondary").Class(":pointerover").Template().OfType<Avalonia.Controls.Presenters.ContentPresenter>())
        {
            Setters =
            {
                new Avalonia.Styling.Setter(Avalonia.Controls.Presenters.ContentPresenter.BackgroundProperty, AndesiteRaised),
                new Avalonia.Styling.Setter(Avalonia.Controls.Presenters.ContentPresenter.BorderBrushProperty, Prada)
            }
        });
        return styles;
    }
}
