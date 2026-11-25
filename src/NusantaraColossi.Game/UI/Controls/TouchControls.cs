using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

using Palette = NusantaraColossi.Game.UI.Theme;

namespace NusantaraColossi.Game.UI.Controls;

/// <summary>
/// Multi-touch layout for phones and tablets: a floating stick on the left half, look-drag on the
/// right half, and thumb buttons. Grip and Stab are hold buttons, like their physical triggers;
/// with hold-to-toggle grip on, a tap on GRIP latches it and the next tap releases it.
/// </summary>
public sealed class TouchControls : Control
{
    private readonly InputMapper input;
    private readonly Dictionary<long, (Kind Kind, Point Origin, Point Last)> touches = [];
    private readonly Action jump, dodge, scan, hack, decoy, lockOn, pause;

    private enum Kind { Stick, Look, Grip, Stab, Button }

    private record struct Pad(string Label, Kind Kind, Action? Press, double X, double Y, double R);

    public TouchControls(InputMapper input, Action jump, Action dodge, Action scan, Action hack, Action decoy, Action lockOn, Action pause)
    {
        this.input = input;
        this.jump = jump; this.dodge = dodge; this.scan = scan; this.hack = hack; this.decoy = decoy; this.lockOn = lockOn; this.pause = pause;
        Background = Brushes.Transparent;
    }

    public IBrush Background { get; set; }

    private static readonly IPen LatchRing = new Pen(new SolidColorBrush(Color.FromArgb(230, 233, 226, 207)), 2, new DashStyle([3, 2], 0));

    private IEnumerable<Pad> Pads()
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        // Right-thumb cluster, measured from the bottom-right corner.
        yield return new("GRIP", Kind.Grip, input.PressGrip, w - 92, h - 100, 50);
        yield return new("STAB", Kind.Stab, null, w - 200, h - 70, 40);
        yield return new("JUMP", Kind.Button, jump, w - 70, h - 210, 34);
        yield return new("DODGE", Kind.Button, dodge, w - 180, h - 170, 30);
        // Ability names, not keyboard keys: touch players never see Q/E/R.
        yield return new(Loc.T("hud.scan").ToUpperInvariant(), Kind.Button, scan, w - 290, h - 50, 26);
        yield return new(Loc.T("hud.hack").ToUpperInvariant(), Kind.Button, hack, w - 300, h - 120, 26);
        yield return new(Loc.T("hud.decoy").ToUpperInvariant(), Kind.Button, decoy, w - 250, h - 180, 26);
        yield return new("LOCK", Kind.Button, lockOn, w - 70, h - 300, 26);
        yield return new("II", Kind.Button, pause, w - 44, 44, 24);
    }

    public override void Render(DrawingContext context)
    {
        context.DrawRectangle(Background, null, new Rect(Bounds.Size));
        var ring = new Pen(new SolidColorBrush(Color.FromArgb(Palette.HighContrast ? (byte)230 : (byte)150, 233, 226, 207)), Palette.HighContrast ? 2 : 1.4);
        foreach (var pad in Pads())
        {
            var held = touches.Values.Any(t => t.Kind == pad.Kind && pad.Kind is Kind.Grip or Kind.Stab)
                    || (pad.Kind == Kind.Grip && input.GripLatched);
            context.DrawEllipse(new SolidColorBrush(held ? Color.FromArgb(140, 201, 162, 75) : Color.FromArgb(Palette.HighContrast ? (byte)170 : (byte)70, 16, 20, 18)), ring, new Point(pad.X, pad.Y), pad.R, pad.R);
            if (pad.Kind == Kind.Grip && input.GripLatched)
                context.DrawEllipse(null, LatchRing, new Point(pad.X, pad.Y), pad.R + 5, pad.R + 5); // latched: second ring, not only fill
            var text = new FormattedText(pad.Label, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(Palette.Telemetry), pad.R > 35 ? 13 : 10.5, Palette.Kapur);
            context.DrawText(text, new Point(pad.X - text.Width / 2, pad.Y - text.Height / 2));
        }
        foreach (var t in touches.Values)
        {
            if (t.Kind != Kind.Stick) continue;
            context.DrawEllipse(null, ring, t.Origin, 58, 58);
            var d = t.Last - t.Origin;
            var len = Math.Sqrt(d.X * d.X + d.Y * d.Y);
            var k = len > 58 ? 58 / len : 1;
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(160, 201, 162, 75)), null, t.Origin + d * k, 24, 24);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Pointer.Type == PointerType.Mouse) return;
        var p = e.GetPosition(this);
        Kind kind = p.X < Bounds.Width * .4 ? Kind.Stick : Kind.Look;
        foreach (var pad in Pads())
        {
            if (Point.Distance(p, new Point(pad.X, pad.Y)) > pad.R + 8) continue;
            kind = pad.Kind;
            pad.Press?.Invoke();
            break;
        }
        touches[e.Pointer.Id] = (kind, p, p);
        e.Pointer.Capture(this);
        Sync();
        // Toggle grip flips on the next simulation tick; redraw shortly after to show the latch.
        if (kind == Kind.Grip) Avalonia.Threading.DispatcherTimer.RunOnce(InvalidateVisual, TimeSpan.FromMilliseconds(60));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!touches.TryGetValue(e.Pointer.Id, out var t)) return;
        var p = e.GetPosition(this);
        if (t.Kind == Kind.Look) input.TouchLook += new Vector2((float)(p.X - t.Last.X), (float)(p.Y - t.Last.Y));
        touches[e.Pointer.Id] = t with { Last = p };
        Sync();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        touches.Remove(e.Pointer.Id);
        Sync();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        touches.Remove(e.Pointer.Id);
        Sync();
    }

    private void Sync()
    {
        var move = Vector2.Zero;
        var grip = false;
        var stab = false;
        foreach (var t in touches.Values)
        {
            switch (t.Kind)
            {
                case Kind.Stick:
                    var d = t.Last - t.Origin;
                    move = new Vector2((float)d.X, (float)-d.Y) / 58f;
                    if (move.LengthSquared() > 1) move = Vector2.Normalize(move);
                    break;
                case Kind.Grip: grip = true; break;
                case Kind.Stab: stab = true; break;
            }
        }
        input.TouchMove = move;
        input.TouchGrip = grip;
        input.TouchAttack = stab;
        InvalidateVisual();
    }
}
