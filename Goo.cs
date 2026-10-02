using System.Windows;
using System.Windows.Media;

namespace DynamicIsland;

/// <summary>
/// The island's black body. The pill and the bubble that splits off it are one piece of liquid: while their
/// round ends are close a neck joins them, thinning as they part until it snaps.
/// </summary>
public sealed class Goo : FrameworkElement
{
    const double Rim = 1;          // the light edge around the body
    const double Tear = 7.5;       // gap between the two round ends at which the neck snaps
    const double Hold = 0.5;       // how far round each end the neck reaches while it is thick
    const double Handle = 2.4;     // how long the neck's curves keep to the direction they leave an end in
    const double Tolerance = 0.02; // of the merged outline

    static readonly Pen Edge = MakeEdge();

    Rect _pill = Rect.Empty, _bubble = Rect.Empty;
    double _radius;

    /// <summary>Where the two are, in this element's own coordinates. An empty bubble is one tucked away out of sight.</summary>
    public void Shape(Rect pill, double radius, Rect bubble)
    {
        if (pill == _pill && radius == _radius && bubble == _bubble) return;
        _pill = pill;
        _radius = radius;
        _bubble = bubble;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_pill.IsEmpty) return;
        Geometry body = Round(_pill, _radius);
        if (_bubble.IsEmpty)
        {
            Draw(dc, body);
            return;
        }

        Geometry bubble = Round(_bubble, _bubble.Height / 2);
        Geometry? neck = Neck();
        if (neck == null && !_pill.IntersectsWith(_bubble))
        {
            Draw(dc, body);
            Draw(dc, bubble);
            return;
        }

        // one outline for the lot, so the rim runs round the whole shape instead of across the joint
        body = Geometry.Combine(body, bubble, GeometryCombineMode.Union, null, Tolerance, ToleranceType.Absolute);
        if (neck != null) body = Geometry.Combine(body, neck, GeometryCombineMode.Union, null, Tolerance, ToleranceType.Absolute);
        Draw(dc, body);
    }

    // the rim is the outer half of a line along the edge: it lightens what is behind the island, not its black
    static void Draw(DrawingContext dc, Geometry body)
    {
        Rect around = body.Bounds;
        around.Inflate(2 * Rim, 2 * Rim);
        var outside = new GeometryGroup { FillRule = FillRule.EvenOdd };
        outside.Children.Add(new RectangleGeometry(around));
        outside.Children.Add(body);

        dc.PushClip(outside);
        dc.DrawGeometry(null, Edge, body);
        dc.Pop();
        dc.DrawGeometry(Brushes.Black, null, body);
    }

    static Geometry Round(Rect rect, double radius)
    {
        rect.Inflate(-Math.Min(Rim, rect.Width / 2), -Math.Min(Rim, rect.Height / 2));
        radius = Math.Max(radius - Rim, 0);
        return new RectangleGeometry(rect, radius, radius);
    }

    /// <summary>The bridge between the pill's top right corner and the bubble's left end, each taken as a circle.</summary>
    Geometry? Neck()
    {
        double r1 = _radius - Rim, r2 = _bubble.Height / 2 - Rim;
        var c1 = new Point(_pill.Right - _radius, _pill.Top + _radius);
        var c2 = new Point(_bubble.Left + _bubble.Height / 2, _bubble.Top + _bubble.Height / 2);
        Vector between = c2 - c1;
        double d = between.Length, gap = d - r1 - r2;
        // still tucked behind the pill, one end inside the other, or pulled clear
        if (r1 <= 0 || r2 <= 0 || between.X <= 0 || d <= Math.Abs(r1 - r2) || gap >= Tear) return null;

        // where the circles cross, as an angle off the line between their centres; none once they have parted
        double u1 = 0, u2 = 0;
        if (gap < 0)
        {
            u1 = Math.Acos(Math.Clamp((r1 * r1 + d * d - r2 * r2) / (2 * r1 * d), -1, 1));
            u2 = Math.Acos(Math.Clamp((r2 * r2 + d * d - r1 * r1) / (2 * r2 * d), -1, 1));
        }

        // the neck lets go of the ends as the gap opens, and is down to nothing at the tear
        double hold = Hold * (1 - Math.Clamp(gap / Tear, 0, 1));
        double axis = Math.Atan2(between.Y, between.X), wide = Math.Acos((r1 - r2) / d);
        double a1 = axis + u1 + (wide - u1) * hold, a2 = axis - u1 - (wide - u1) * hold;
        double a3 = axis + Math.PI - u2 - (Math.PI - u2 - wide) * hold, a4 = axis - Math.PI + u2 + (Math.PI - u2 - wide) * hold;
        Point p1 = On(c1, a1, r1), p2 = On(c1, a2, r1), p3 = On(c2, a3, r2), p4 = On(c2, a4, r2);

        double reach = Math.Min(hold * Handle, (p1 - p3).Length / (r1 + r2)) * Math.Min(1, 2 * d / (r1 + r2));
        const double Quarter = Math.PI / 2;
        var neck = new StreamGeometry();
        using (StreamGeometryContext g = neck.Open())
        {
            g.BeginFigure(p1, true, true);
            g.BezierTo(On(p1, a1 - Quarter, r1 * reach), On(p3, a3 + Quarter, r2 * reach), p3, true, true);
            g.LineTo(p4, true, true);
            g.BezierTo(On(p4, a4 - Quarter, r2 * reach), On(p2, a2 + Quarter, r1 * reach), p2, true, true);
        }
        return neck;
    }

    static Point On(Point centre, double angle, double radius) =>
        new(centre.X + radius * Math.Cos(angle), centre.Y + radius * Math.Sin(angle));

    static Pen MakeEdge()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)), 2 * Rim) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        return pen;
    }
}
