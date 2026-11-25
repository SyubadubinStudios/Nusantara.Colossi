using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using NusantaraColossi.Core;

using Palette = NusantaraColossi.Game.UI.Theme;

namespace NusantaraColossi.Game.UI.Controls;

/// <summary>
/// A navigator's chart of the archipelago: island outlines drawn as contour rings (like an old
/// survey map), a lat/long hairline grid, and one diamond per guardian. Beaten guardians are
/// joined by a gold route, so the map records the expedition.
/// </summary>
public sealed class ArchipelagoMap : Control
{
    // Stylised island outlines in map space (0..1), matching RegionDefinition.MapPosition.
    private static readonly Point[][] Islands =
    [
        P(.03, .13, .08, .16, .15, .29, .22, .43, .255, .53, .225, .565, .16, .48, .09, .36, .03, .22), // Sumatra
        P(.235, .565, .3, .57, .36, .6, .412, .625, .405, .662, .35, .656, .29, .627, .245, .6), // Java
        P(.425, .634, .446, .638, .444, .655, .424, .652), // Bali
        P(.452, .64, .47, .642, .468, .66, .452, .656), // Lombok
        P(.475, .645, .522, .645, .524, .664, .476, .666), // Sumbawa
        P(.53, .658, .6, .662, .602, .684, .532, .68), // Flores
        P(.615, .72, .695, .69, .702, .708, .628, .742), // Timor
        P(.33, .25, .4, .17, .48, .19, .525, .3, .505, .44, .44, .5, .36, .48, .32, .38), // Kalimantan
        P(.56, .27, .625, .255, .645, .3, .592, .33, .6, .4, .665, .415, .635, .46, .592, .44, .582, .54, .552, .52, .556, .4), // Sulawesi
        P(.695, .29, .72, .27, .732, .38, .708, .4), // Halmahera
        P(.675, .468, .76, .458, .762, .482, .678, .492), // Seram
        P(.712, .515, .722, .512, .724, .526, .713, .528), // Banda
        P(.775, .38, .83, .35, .87, .36, .94, .41, 1.0, .47, 1.0, .72, .93, .64, .87, .56, .82, .5, .79, .45), // Papua
        P(.8, .365, .812, .358, .815, .375, .802, .378), // Waigeo (Raja Ampat)
    ];

    private static Point[] P(params double[] v)
    {
        var points = new Point[v.Length / 2];
        for (var i = 0; i < points.Length; i++) points[i] = new Point(v[i * 2], v[i * 2 + 1]);
        return points;
    }

    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 233, 226, 207)), .6);
    private static readonly IPen CoastPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 201, 162, 75)), 1.1);
    private static readonly IPen ContourPen = new Pen(new SolidColorBrush(Color.FromArgb(55, 201, 162, 75)), .7);
    private static readonly IBrush LandFill = new SolidColorBrush(Color.FromArgb(255, 36, 48, 40));
    private static readonly IPen RoutePen = new Pen(Palette.Prada, 1.4, new DashStyle([4, 4], 0));
    private static readonly IPen MarkerPen = new Pen(Palette.Prada, 1.5);
    private static readonly IPen LockedPen = new Pen(Palette.Hairline, 1.2);
    private static readonly IPen SelectPen = new Pen(Palette.Nadi, 1.5);

    public Profile? Profile { get; set; }
    public RegionId Selected { get; set; }
    public event Action<RegionId>? SelectionChanged;

    private double pulse;

    public ArchipelagoMap()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public void Tick(float dt)
    {
        pulse += dt;
        InvalidateVisual();
    }

    private Rect Area()
    {
        // Keep the chart's aspect (≈2.1:1) and centre it.
        var b = Bounds;
        var w = Math.Min(b.Width - 24, (b.Height - 24) * 2.1);
        var h = w / 2.1;
        return new Rect((b.Width - w) / 2, (b.Height - h) / 2, w, h);
    }

    private static Point Map(Rect a, double x, double y) => new(a.X + x * a.Width, a.Y + y * a.Height);

    public override void Render(DrawingContext context)
    {
        var a = Area();
        // Graticule.
        for (var i = 0; i <= 12; i++)
        {
            var x = a.X + a.Width * i / 12;
            context.DrawLine(GridPen, new Point(x, a.Y), new Point(x, a.Bottom));
        }
        for (var i = 0; i <= 6; i++)
        {
            var y = a.Y + a.Height * i / 6;
            context.DrawLine(GridPen, new Point(a.X, y), new Point(a.Right, y));
        }

        // Islands with two offset contour rings, then the coast.
        foreach (var island in Islands)
        {
            var cx = island.Average(p => p.X);
            var cy = island.Average(p => p.Y);
            for (var ring = 2; ring >= 1; ring--)
            {
                var s = 1 + ring * .035;
                context.DrawGeometry(null, ContourPen, Polygon(island, p => Map(a, cx + (p.X - cx) * s, cy + (p.Y - cy) * s + ring * .004)));
            }
            context.DrawGeometry(LandFill, CoastPen, Polygon(island, p => Map(a, p.X, p.Y)));
        }

        // Expedition route through beaten guardians, in canonical order.
        Point? previous = null;
        foreach (var region in Regions.All)
        {
            if (Profile?.IsDefeated(region.ColossusId) != true) continue;
            var p = Map(a, region.MapPosition.X, region.MapPosition.Y);
            if (previous is { } prev) context.DrawLine(RoutePen, prev, p);
            previous = p;
        }

        foreach (var region in Regions.All)
        {
            var p = Map(a, region.MapPosition.X, region.MapPosition.Y);
            var unlocked = Profile?.IsUnlocked(region.Id) ?? true;
            var defeated = Profile?.IsDefeated(region.ColossusId) ?? false;
            var size = 7.0;
            var diamond = Diamond(p, size);
            context.DrawGeometry(defeated ? Palette.Prada : unlocked ? Palette.Andesite : Palette.AndesiteRaised, unlocked ? MarkerPen : LockedPen, diamond);
            if (!defeated && unlocked) context.DrawGeometry(Palette.Bara, null, Diamond(p, 2.4));
            if (region.Id == Selected)
            {
                var r = 13 + 5 * (.5 + .5 * Math.Sin(pulse * 3));
                context.DrawGeometry(null, SelectPen, Diamond(p, r));
                context.DrawLine(SelectPen, new Point(p.X + 22, p.Y), new Point(p.X + 46, p.Y));
            }
            var label = new FormattedText(region.Name.Get().ToUpperInvariant(), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(Palette.Inscription), 12, region.Id == Selected ? Palette.PradaBright : unlocked ? Palette.Kapur : Palette.KapurMuted);
            // Java's guardians sit close together; their labels fan out so none collide.
            var at = region.Id switch
            {
                RegionId.Jakarta => new Point(p.X - label.Width - 12, p.Y - label.Height / 2),
                RegionId.Bromo => new Point(p.X - label.Width / 2, p.Y - label.Height - 12),
                RegionId.Bali => new Point(p.X + 12, p.Y - label.Height - 6),
                _ => new Point(p.X - label.Width / 2, p.Y + 13),
            };
            context.DrawText(label, at);
        }
    }

    private static StreamGeometry Polygon(Point[] points, Func<Point, Point> map)
    {
        var g = new StreamGeometry();
        using var c = g.Open();
        c.BeginFigure(map(points[0]), true);
        // Quadratic smoothing through midpoints softens the hand-placed vertices into coastlines.
        for (var i = 0; i < points.Length; i++)
        {
            var p0 = map(points[i]);
            var p1 = map(points[(i + 1) % points.Length]);
            c.QuadraticBezierTo(p0, new Point((p0.X + p1.X) / 2, (p0.Y + p1.Y) / 2));
        }
        c.EndFigure(true);
        return g;
    }

    private static StreamGeometry Diamond(Point p, double r)
    {
        var g = new StreamGeometry();
        using var c = g.Open();
        c.BeginFigure(new Point(p.X, p.Y - r), true);
        c.LineTo(new Point(p.X + r, p.Y));
        c.LineTo(new Point(p.X, p.Y + r));
        c.LineTo(new Point(p.X - r, p.Y));
        c.EndFigure(true);
        return g;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var a = Area();
        var pos = e.GetPosition(this);
        RegionId? best = null;
        var bestDistance = 40.0;
        foreach (var region in Regions.All)
        {
            var p = Map(a, region.MapPosition.X, region.MapPosition.Y);
            var d = Point.Distance(p, pos);
            if (d < bestDistance) { bestDistance = d; best = region.Id; }
        }
        if (best is { } id)
        {
            Selected = id;
            Palette.Sound?.Invoke("ui-click");
            SelectionChanged?.Invoke(id);
            InvalidateVisual();
        }
    }
}
