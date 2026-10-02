using System.Windows;
using System.Windows.Media;

namespace DynamicIsland;

public enum Glyph { Mute, Quiet, Mid, Loud, Headphones, Speaker, Vpn, Offline, Wifi, Wired, Bell, Note, Battery, Minus, Plus, Chevron }

/// <summary>
/// The island's own icons: filled shapes with rounded corners on a 24-unit grid, in the manner of the
/// player's buttons, so nothing depends on which icon font the system has.
/// </summary>
public sealed class Icon : FrameworkElement
{
    const double Grid = 24;
    const double Soft = 1.5;       // outline that rounds the corners of a solid shape off
    const double Tolerance = 0.01; // of the curves, in grid units

    /// <param name="Solid">Closed shapes, filled.</param>
    /// <param name="Lines">Strokes with round ends, <paramref name="Line"/> thick.</param>
    /// <param name="Cut">Strokes taken out of all that, <paramref name="Gap"/> thick.</param>
    /// <param name="Over">Strokes put back over the cut.</param>
    readonly record struct Art(string Solid = "", string Lines = "", double Line = 2, string Cut = "", double Gap = 2, string Over = "");

    // the speaker keeps its place while the waves come and go with the volume
    const string Horn = "M2.5,9.6 H6 L10.6,5.6 V18.4 L6,14.4 H2.5 Z";
    const string Wave1 = "M13.3,9.3 A3.8,3.8 0 0 1 13.3,14.7", Wave2 = " M15.7,6.9 A7.2,7.2 0 0 1 15.7,17.1", Wave3 = " M18.1,4.5 A10.6,10.6 0 0 1 18.1,19.5";
    const string Fan = "M2.3,9 A13.7,13.7 0 0 1 21.7,9 M5.4,12.1 A9.3,9.3 0 0 1 18.6,12.1 M8.5,15.2 A4.9,4.9 0 0 1 15.5,15.2";
    const string Dot = "M12,17.7 A1,1 0 1 0 12,19.7 A1,1 0 1 0 12,17.7 Z";
    const string Slash = "M4,3.5 L20,20.5";

    static readonly Dictionary<Glyph, Art> Arts = new()
    {
        [Glyph.Mute] = new(Horn, "M14.8,9.2 L20.4,14.8 M20.4,9.2 L14.8,14.8"),
        [Glyph.Quiet] = new(Horn, Wave1),
        [Glyph.Mid] = new(Horn, Wave1 + Wave2),
        [Glyph.Loud] = new(Horn, Wave1 + Wave2 + Wave3),
        [Glyph.Headphones] = new("M3.6,14 H7 V19.6 H3.6 Z M17,14 H20.4 V19.6 H17 Z", "M4.6,14 V12.2 A7.4,7.4 0 0 1 19.4,12.2 V14"),
        // a box with its two drivers left open
        [Glyph.Speaker] = new("F0 M7.2,3.4 H16.8 V20.6 H7.2 Z M12,5.4 A2.1,2.1 0 1 0 12,9.6 A2.1,2.1 0 1 0 12,5.4 Z"
            + " M12,11 A3.9,3.9 0 1 0 12,18.8 A3.9,3.9 0 1 0 12,11 Z"),
        [Glyph.Vpn] = new("M12,2.8 L19.6,5.6 V11.4 C19.6,16.2 16.4,19.6 12,21.4 C7.6,19.6 4.4,16.2 4.4,11.4 V5.6 Z",
            Cut: "M8.6,11.9 L11,14.3 L15.6,9.4"),
        [Glyph.Offline] = new(Dot, Fan, 2.2, Slash, 5.4, Slash),
        [Glyph.Wifi] = new(Dot, Fan, 2.2),
        // the plug, pins up
        [Glyph.Wired] = new("M4.5,6.5 H19.5 V14.5 H16 V18 H8 V14.5 H4.5 Z", Cut: "M8.5,6 V9.6 M12,6 V9.6 M15.5,6 V9.6", Gap: 1.5),
        [Glyph.Bell] = new("M12,3.2 C8.6,3.2 6.6,5.8 6.6,9.2 V12.8 L4.8,16.2 H19.2 L17.4,12.8 V9.2 C17.4,5.8 15.4,3.2 12,3.2 Z"
            + " M10,18.9 A2,2 0 0 0 14,18.9 Z"),
        [Glyph.Note] = new("M7.2,15 A2.5,2.5 0 1 0 7.2,20 A2.5,2.5 0 1 0 7.2,15 Z M16.6,13 A2.5,2.5 0 1 0 16.6,18 A2.5,2.5 0 1 0 16.6,13 Z"
            + " M9.2,5.4 L18.6,3.4 V6.8 L9.2,8.8 Z", "M9.3,17.5 V6 M18.7,15.5 V4", 1.8),
        [Glyph.Battery] = new("M5.1,10.7 H16.5 V13.3 H5.1 Z",
            "M4.2,7.6 H17.4 A2.2,2.2 0 0 1 19.6,9.8 V14.2 A2.2,2.2 0 0 1 17.4,16.4 H4.2 A2.2,2.2 0 0 1 2,14.2 V9.8 A2.2,2.2 0 0 1 4.2,7.6 Z"
            + " M21.9,10.7 V13.3", 1.5),
        [Glyph.Minus] = new(Lines: "M5.5,12 H18.5", Line: 2.4),
        [Glyph.Plus] = new(Lines: "M5.5,12 H18.5 M12,5.5 V18.5", Line: 2.4),
        [Glyph.Chevron] = new(Lines: "M9,5 L16,12 L9,19", Line: 2.6),
    };

    static readonly Dictionary<Glyph, Geometry> Shapes = new();

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(Glyph), typeof(Icon),
        new FrameworkPropertyMetadata(Glyph.Note, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(Icon),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public Glyph Kind
    {
        get => (Glyph)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;

        if (!Shapes.TryGetValue(Kind, out Geometry? shape)) Shapes[Kind] = shape = Build(Arts[Kind]);
        dc.PushTransform(new TranslateTransform((ActualWidth - size) / 2, (ActualHeight - size) / 2));
        dc.PushTransform(new ScaleTransform(size / Grid, size / Grid));
        dc.DrawGeometry(Fill, null, shape);
        dc.Pop();
        dc.Pop();
    }

    // everything ends up in one outline: a see-through brush would show where a fill and its stroke overlap
    static Geometry Build(Art art)
    {
        Geometry shape = Geometry.Empty;
        if (art.Solid.Length > 0)
        {
            Geometry solid = Geometry.Parse(art.Solid);
            shape = Join(solid, Stroke(solid, Soft), GeometryCombineMode.Union);
        }
        if (art.Lines.Length > 0) shape = Join(shape, Stroke(Geometry.Parse(art.Lines), art.Line), GeometryCombineMode.Union);
        if (art.Cut.Length > 0) shape = Join(shape, Stroke(Geometry.Parse(art.Cut), art.Gap), GeometryCombineMode.Exclude);
        if (art.Over.Length > 0) shape = Join(shape, Stroke(Geometry.Parse(art.Over), art.Line), GeometryCombineMode.Union);
        shape.Freeze();
        return shape;
    }

    static Geometry Stroke(Geometry path, double thickness) => path.GetWidenedPathGeometry(
        new Pen(Brushes.Black, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round },
        Tolerance, ToleranceType.Absolute);

    static Geometry Join(Geometry a, Geometry b, GeometryCombineMode mode) =>
        Geometry.Combine(a, b, mode, null, Tolerance, ToleranceType.Absolute);
}
