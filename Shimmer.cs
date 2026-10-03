using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DynamicIsland;

/// <summary>
/// Stands in for the lyrics while they are looked up: three faint lines where theirs will be, with a sheen in the
/// colours of the cover sliding across them at a slant, so each row catches the light a moment after the one above.
/// </summary>
public sealed class Shimmer : FrameworkElement
{
    const double Row = 18, Gap = 4, Bar = 10; // the rows of the lyrics, and how thick a line of them is drawn
    const double Band = 170;                  // width of the sheen
    const double Slant = 0.35;                // how far it leans: each row lower is lit this much of a row later
    static readonly TimeSpan Pass = TimeSpan.FromSeconds(1.5), Rest = TimeSpan.FromSeconds(0.6);

    // the line being sung in the middle, its neighbours shorter and dimmer: (share of the width, opacity)
    static readonly (double Width, double Alpha)[] Rows = { (0.58, 0.55), (0.84, 1), (0.42, 0.55) };

    readonly SolidColorBrush _base = new(Color.FromArgb(26, 255, 255, 255));
    readonly LinearGradientBrush _sheen;
    readonly TranslateTransform _move = new();
    bool _running;

    public Shimmer()
    {
        // across the band: nothing, the cover's colour, nearly white at the crest, its second colour, nothing
        _sheen = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(0, 0),
            EndPoint = new Point(Band, Band * Slant),
            Transform = _move,
        };
        foreach (var (color, offset) in new[]
                 {
                     (Colors.Transparent, 0.0), (Fade(Colors.White, 0.35), 0.3), (Fade(Colors.White, 0.8), 0.5),
                     (Fade(Colors.White, 0.35), 0.7), (Colors.Transparent, 1.0),
                 })
            _sheen.GradientStops.Add(new GradientStop(color, offset));
        _move.X = -Band * 2;
    }

    /// <summary>Turns the sheen to the colours of another cover, lifted towards white so it reads as light on the black.</summary>
    public void Tint(IReadOnlyList<Color> colors, Duration time)
    {
        Color a = Lift(colors[0]), b = Lift(colors[Math.Min(1, colors.Count - 1)]);
        Color[] stops = { Colors.Transparent, Fade(a, 0.45), Fade(Mix(a, Colors.White, 0.6), 0.85), Fade(b, 0.45), Colors.Transparent };
        for (int i = 0; i < stops.Length; i++)
            _sheen.GradientStops[i].BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(stops[i], time));
    }

    /// <summary>Sets the sheen going, or stops it: it only costs frames while there is something to wait for.</summary>
    public void Run(bool on)
    {
        if (on == _running) return;
        _running = on;
        if (!on)
        {
            _move.BeginAnimation(TranslateTransform.XProperty, null);
            return;
        }

        // from out of sight on the left to out of sight on the right, then a breath before the next pass
        double from = -Band - Rows.Length * (Row + Gap) * Slant, to = (double.IsNaN(Width) ? ActualWidth : Width) + Band;
        var sweep = new DoubleAnimationUsingKeyFrames { Duration = Pass + Rest, RepeatBehavior = RepeatBehavior.Forever };
        sweep.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        sweep.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(Pass), new SineEase { EasingMode = EasingMode.EaseInOut }));
        sweep.KeyFrames.Add(new DiscreteDoubleKeyFrame(to, KeyTime.FromTimeSpan(Pass + Rest)));
        _move.BeginAnimation(TranslateTransform.XProperty, sweep);
    }

    protected override Size MeasureOverride(Size available) =>
        new(double.IsInfinity(available.Width) ? 0 : available.Width, Rows.Length * Row + (Rows.Length - 1) * Gap);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, top = (ActualHeight - (Rows.Length * Row + (Rows.Length - 1) * Gap)) / 2;
        if (w <= 0) return;

        for (int i = 0; i < Rows.Length; i++)
        {
            var bar = new Rect(0, top + i * (Row + Gap) + (Row - Bar) / 2, Math.Round(w * Rows[i].Width), Bar);
            dc.PushOpacity(Rows[i].Alpha);
            // the sheen is laid over the whole box, so it runs through the rows as one slanted band
            dc.DrawRoundedRectangle(_base, null, bar, Bar / 2, Bar / 2);
            dc.DrawRoundedRectangle(_sheen, null, bar, Bar / 2, Bar / 2);
            dc.Pop();
        }
    }

    static Color Fade(Color c, double alpha) => Color.FromArgb((byte)(255 * alpha), c.R, c.G, c.B);

    static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    // a dark cover still gives a sheen that shows
    static Color Lift(Color c) => Mix(c, Colors.White, 0.35);
}
