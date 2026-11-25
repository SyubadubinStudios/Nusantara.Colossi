using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using NusantaraColossi.Core;

using Palette = NusantaraColossi.Game.UI.Theme;

namespace NusantaraColossi.Game.UI.Controls;

/// <summary>
/// The HUD signature: a kawung (four-petal batik) mark inside a stamina ring. The petals drain as
/// grip stamina falls and turn ember below 30 %. While a stab charges, an inner cyan arc fills,
/// and a dot beats with the nearest core's pulse: release on the beat for resonance.
/// Colour-blind safety: low stamina is never colour alone. The ring also breaks into dashes,
/// flashes and shows a "!" notch (gold vs ember is only dE 9.7 under deuteranopia).
/// </summary>
public sealed class GripRing : Control
{
    public float Stamina { get; set; } = 1;
    public float Charge { get; set; }
    public float Pulse { get; set; }
    public bool ShowPulse { get; set; }
    public float Time { get; set; }
    /// <summary>Hold-to-toggle grip is latched on: a small lock bar under the ring.</summary>
    public bool Latched { get; set; }

    private static readonly IPen Track = new Pen(new SolidColorBrush(Color.FromArgb(90, 233, 226, 207)), 3);
    private static readonly IPen TrackHighContrast = new Pen(new SolidColorBrush(Color.FromArgb(170, 233, 226, 207)), 3);
    private static readonly IBrush PetalEmpty = new SolidColorBrush(Color.FromArgb(70, 233, 226, 207));
    private static readonly DashStyle LowDash = new([1.6, 1.1], 0);

    public GripRing()
    {
        Width = Height = 112;
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var r = Math.Min(Bounds.Width, Bounds.Height) / 2 - 6;
        var low = Stamina < .3f;
        var flash = low ? .55 + .45 * Math.Sin(Time * 14) : 1;
        var color = low ? Palette.Danger : Palette.PradaColor;
        // Low stamina: dashed ring (shape cue) on top of the ember colour and the flash.
        var ring = new Pen(new SolidColorBrush(color, flash), Palette.HighContrast ? 6 : 5, low ? LowDash : null, low ? PenLineCap.Flat : PenLineCap.Round);

        context.DrawEllipse(null, Palette.HighContrast ? TrackHighContrast : Track, c, r, r);
        Arc(context, ring, c, r, Stamina);
        if (low)
        {
            // "!" notch above the ring.
            var bang = new SolidColorBrush(color, flash);
            context.DrawRectangle(bang, null, new Rect(c.X - 1.5, 0, 3, 7));
            context.DrawRectangle(bang, null, new Rect(c.X - 1.5, 9, 3, 3));
        }
        if (Latched)
        {
            // Toggle grip latched: a solid bar with two rivets under the ring.
            var y = Bounds.Height - 4;
            context.DrawRectangle(Palette.Kapur, null, new Rect(c.X - 14, y - 3, 28, 4), 1, 1);
            context.DrawRectangle(Palette.Andesite, null, new Rect(c.X - 9, y - 2, 2, 2));
            context.DrawRectangle(Palette.Andesite, null, new Rect(c.X + 7, y - 2, 2, 2));
        }

        // Kawung: four ellipse petals around the centre, filled in proportion to stamina.
        var petal = r * .34;
        for (var i = 0; i < 4; i++)
        {
            var angle = Math.PI / 4 + i * Math.PI / 2;
            var center = new Point(c.X + Math.Cos(angle) * petal * .9, c.Y + Math.Sin(angle) * petal * .9);
            var lit = Stamina * 4 - i;
            using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateRotation(angle) * Matrix.CreateTranslation(center.X, center.Y)))
            {
                context.DrawEllipse(PetalEmpty, null, center, petal * .78, petal * .42);
                if (lit > 0)
                    context.DrawEllipse(new SolidColorBrush(color, Math.Clamp(lit, 0, 1) * flash), null, center, petal * .78, petal * .42);
            }
        }

        if (Charge > 0)
        {
            var inner = new Pen(Palette.Nadi, 3, lineCap: PenLineCap.Round);
            Arc(context, inner, c, r - 10, Charge);
        }
        if (ShowPulse)
        {
            var beat = Pulse > .5f;
            var size = 3 + Pulse * 5;
            context.DrawEllipse(beat ? Palette.Nadi : Palette.KapurMuted, null, c, size, size);
            if (beat && Charge > .2f) context.DrawEllipse(null, new Pen(Palette.Nadi, 1.5), c, r - 18, r - 18);
        }
    }

    private static void Arc(DrawingContext context, IPen pen, Point c, double r, float fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        if (fraction <= .001f) return;
        if (fraction >= .999f)
        {
            context.DrawEllipse(null, pen, c, r, r);
            return;
        }
        var start = -Math.PI / 2;
        var end = start + fraction * Math.PI * 2;
        var g = new StreamGeometry();
        using (var s = g.Open())
        {
            s.BeginFigure(new Point(c.X + Math.Cos(start) * r, c.Y + Math.Sin(start) * r), false);
            s.ArcTo(new Point(c.X + Math.Cos(end) * r, c.Y + Math.Sin(end) * r), new Size(r, r), 0, fraction > .5f, SweepDirection.Clockwise);
            s.EndFigure(false);
        }
        context.DrawGeometry(null, pen, g);
    }
}

/// <summary>
/// Guardian vitality drawn as a candi: one stone tier per core, stacked like a temple's roof.
/// Gold = intact, cyan = seal open (strike now), dark and cracked = destroyed. Rage adds ember.
/// Every state also has a shape: sealed = lock notch, plated = two plate bars, open = inward
/// arrowheads on both ends, destroyed = cracks, rage = ember pips and a hatched plinth.
/// </summary>
public sealed class CandiVitality : Control
{
    public Encounter? Encounter { get; set; }
    public float Time { get; set; }

    private static readonly IPen Outline = new Pen(Palette.Prada, 1.2);
    private static readonly IPen Crack = new Pen(Palette.Andesite, 1.4);
    private static readonly IBrush Dead = new SolidColorBrush(Color.FromArgb(160, 31, 38, 35));

    public CandiVitality()
    {
        Width = 300;
        Height = 76;
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        if (Encounter is not { } e) return;
        var cores = e.Colossus.Cores;
        var n = cores.Length;
        var w = Bounds.Width;
        var baseY = Bounds.Height - 12;
        var tierH = Math.Min(14, (Bounds.Height - 26) / n);
        var rage = e.Colossus.Rage;

        for (var i = 0; i < n; i++)
        {
            // Bottom tier is widest; each one steps in like a candi roof.
            var tw = w * (.62 - i * (.42 / Math.Max(1, n)));
            var y = baseY - (i + 1) * (tierH + 3);
            var rect = new Rect((w - tw) / 2, y, tw, tierH);
            var core = cores[i];
            IBrush fill;
            if (core.Destroyed) fill = Dead;
            else if (core.SealOpen > 0)
            {
                var beat = e.Colossus.CorePulse(e.Elapsed);
                fill = new SolidColorBrush(Palette.NadiColor, .55 + .45 * beat);
            }
            else
            {
                var health = Math.Clamp(core.Health / Math.Max(1, e.Blueprint.Cores[i].Health), 0, 1);
                fill = new SolidColorBrush(Palette.PradaColor, .35 + .65 * health);
            }
            context.DrawRectangle(fill, Outline, rect, 1.5, 1.5);
            if (!core.Destroyed && core.SealOpen > 0)
            {
                // Open: arrowheads pointing at the tier from both sides ("strike here").
                var my = rect.Y + rect.Height / 2;
                var a = tierH * .45;
                DrawArrow(context, new Point(rect.X - 3, my), -1, a);
                DrawArrow(context, new Point(rect.Right + 3, my), 1, a);
            }
            else if (!core.Destroyed && e.Blueprint.Cores[i].Plated && e.Colossus.State != ColossusState.Stunned)
            {
                // Plated: two dark plate bars across the tier.
                context.DrawRectangle(Palette.Andesite, null, new Rect(rect.X + rect.Width * .18, rect.Y + 2, rect.Width * .64, 2));
                context.DrawRectangle(Palette.Andesite, null, new Rect(rect.X + rect.Width * .18, rect.Bottom - 4, rect.Width * .64, 2));
            }
            if (core.Destroyed)
            {
                context.DrawLine(Crack, new Point(rect.X + rect.Width * .3, rect.Y), new Point(rect.X + rect.Width * .42, rect.Bottom));
                context.DrawLine(Crack, new Point(rect.X + rect.Width * .62, rect.Y), new Point(rect.X + rect.Width * .55, rect.Bottom));
            }
            else if (!core.Revealed && e.Options.Legend == false && core.SealOpen <= 0)
            {
                // Sealed: a small lock notch.
                var cx = rect.X + rect.Width / 2;
                context.DrawRectangle(Palette.Andesite, null, new Rect(cx - 3, rect.Y + 3, 6, rect.Height - 6));
            }
        }

        // Plinth: whole-guardian vitality and rage marks.
        var plinth = new Rect(w * .12, baseY, w * .76, 5);
        context.DrawRectangle(Dead, null, plinth);
        var filled = plinth.WithWidth(plinth.Width * Math.Clamp(e.Colossus.Vitality, 0, 1));
        context.DrawRectangle(rage > 0 ? Palette.Bara : Palette.Prada, null, filled);
        if (rage > 0)
        {
            // Rage hatching: dark diagonal ticks across the plinth, so rage is not hue-only.
            using (context.PushClip(filled))
                for (var x = filled.X - 6; x < filled.Right; x += 7)
                    context.DrawLine(Hatch, new Point(x, filled.Bottom), new Point(x + 5, filled.Y));
        }
        for (var r = 0; r < rage; r++)
        {
            var x = w * .12 - 12 - r * 9;
            var glow = .6 + .4 * Math.Sin(Time * 6 + r);
            context.DrawRectangle(new SolidColorBrush(Palette.Danger, glow), null, new Rect(x, baseY - 1, 6, 7));
        }
    }

    private static readonly IPen Hatch = new Pen(Palette.Andesite, 1.4);

    private static void DrawArrow(DrawingContext context, Point tip, int side, double size)
    {
        // side -1 = left of the tier (points right), +1 = right of the tier (points left).
        var g = new StreamGeometry();
        using (var s = g.Open())
        {
            s.BeginFigure(tip, true);
            s.LineTo(new Point(tip.X + side * size * 1.3, tip.Y - size));
            s.LineTo(new Point(tip.X + side * size * 1.3, tip.Y + size));
            s.EndFigure(true);
        }
        context.DrawGeometry(Palette.Kapur, null, g);
    }
}

/// <summary>
/// One GARUDA ability: a key label, name and a cooldown sweep. Ready is shown by a bright frame
/// and a filled corner diamond (not only the cyan colour); locked by the "LOCKED" label.
/// </summary>
public sealed class AbilitySlot : Control
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public float Cooldown { get; set; }
    public bool Locked { get; set; }

    public AbilitySlot()
    {
        Width = 74;
        Height = 74;
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(0, 0, Bounds.Width, Bounds.Width);
        var ready = !Locked && Cooldown <= 0;
        var frame = new Pen(ready ? Palette.Nadi : Palette.Hairline, Palette.HighContrast ? 2 : 1.2);
        // Clipped-corner frame reads as GARUDA hardware rather than temple stone.
        var g = new StreamGeometry();
        using (var s = g.Open())
        {
            const double k = 10;
            s.BeginFigure(new Point(k, 0), true);
            s.LineTo(new Point(rect.Width, 0));
            s.LineTo(new Point(rect.Width, rect.Height - k));
            s.LineTo(new Point(rect.Width - k, rect.Height));
            s.LineTo(new Point(0, rect.Height));
            s.LineTo(new Point(0, k));
            s.EndFigure(true);
        }
        context.DrawGeometry(Palette.Scrim, frame, g);
        if (!Locked && Cooldown > 0)
        {
            var h = rect.Height * Math.Clamp(Cooldown, 0, 1);
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(120, 63, 214, 207)), null, new Rect(1, rect.Height - h, rect.Width - 2, h));
        }
        if (ready)
        {
            // Ready diamond in the top-right corner.
            var d = new StreamGeometry();
            using (var s = d.Open())
            {
                s.BeginFigure(new Point(rect.Width - 9, 3), true);
                s.LineTo(new Point(rect.Width - 4, 8));
                s.LineTo(new Point(rect.Width - 9, 13));
                s.LineTo(new Point(rect.Width - 14, 8));
                s.EndFigure(true);
            }
            context.DrawGeometry(Palette.Nadi, null, d);
        }
        var keyText = new FormattedText(Key, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(Palette.Telemetry), 18, ready ? Palette.Kapur : Palette.KapurMuted);
        context.DrawText(keyText, new Point((rect.Width - keyText.Width) / 2, 14));
        var label = new FormattedText(Locked ? Loc.T("hud.locked") : Label.ToUpperInvariant(), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(Palette.Telemetry), 9.5, ready ? Palette.Nadi : Palette.KapurMuted);
        context.DrawText(label, new Point((rect.Width - label.Width) / 2, rect.Height - 22));
    }
}

/// <summary>Thin segmented health bar. Below 30 % it turns ember and also blinks an outline, so low health is not hue-only.</summary>
public sealed class HealthBar : Control
{
    public float Value { get; set; } = 1;
    public float Flash { get; set; }

    private static readonly IPen LowOutline = new Pen(new SolidColorBrush(Color.FromArgb(230, 233, 226, 207)), 1.2);

    public HealthBar()
    {
        Width = 260;
        Height = 8;
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        const int segments = 10;
        var gap = 3.0;
        var sw = (w - gap * (segments - 1)) / segments;
        for (var i = 0; i < segments; i++)
        {
            var fill = Math.Clamp(Value * segments - i, 0, 1);
            var x = i * (sw + gap);
            context.DrawRectangle(Palette.AndesiteRaised, null, new Rect(x, 0, sw, Bounds.Height));
            if (fill > 0)
                context.DrawRectangle(Value < .3f ? Palette.Bara : Flash > 0 ? Palette.Kapur : Palette.PradaBright, null, new Rect(x, 0, sw * fill, Bounds.Height));
        }
        if (Value < .3f && Value > 0 && Environment.TickCount64 / 300 % 2 == 0)
            context.DrawRectangle(null, LowOutline, new Rect(-2, -2, Math.Max(sw, (sw + gap) * Math.Ceiling(Value * segments) - gap) + 4, Bounds.Height + 4));
    }
}
