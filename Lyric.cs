using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DynamicIsland;

/// <summary>
/// A line of the lyrics in the expanded player: it wraps once, a longer one is cut. While it is sung it fills
/// with light from its first letter to its last, the second row of a wrapped line after the first.
/// </summary>
public sealed class Lyric : FrameworkElement
{
    const double FontSize = 14, LineHeight = 18;
    const int MaxLines = 2;
    const double Edge = 26; // width of the soft boundary between what has been sung and what has not

    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(Lyric), new FrameworkPropertyMetadata(0.0, (d, _) => ((Lyric)d).Sweep()));

    public static readonly DependencyProperty UnsungProperty = DependencyProperty.Register(
        nameof(Unsung), typeof(double), typeof(Lyric),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((Lyric)d).Sweep()));

    readonly string _words;
    FormattedText? _text;
    double[] _rows = [];                // how far the words reach in each row of the line
    LinearGradientBrush[] _masks = [];  // one per row: opaque up to the boundary, Unsung past it

    public Lyric(string words) => _words = words;

    /// <summary>How much of the line has been sung, 0 → 1.</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>Opacity of the part not sung yet. At 1 the line is evenly lit, whatever its progress.</summary>
    public double Unsung
    {
        get => (double)GetValue(UnsungProperty);
        set => SetValue(UnsungProperty, value);
    }

    protected override Size MeasureOverride(Size available)
    {
        double width = double.IsInfinity(available.Width) ? 0 : available.Width;
        var face = new Typeface((FontFamily)GetValue(TextElement.FontFamilyProperty), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        _text = new FormattedText(_words, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, FontSize, Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(width, 1),
            MaxLineCount = MaxLines,
            Trimming = TextTrimming.CharacterEllipsis,
            LineHeight = LineHeight,
        };

        int rows = Math.Clamp((int)Math.Round(_text.Height / LineHeight), 1, MaxLines);
        _rows = new double[rows];
        _masks = new LinearGradientBrush[rows];
        // the highlight is a box per row, each as wide as its words: the top edge of the lot belongs to the first
        // row and the bottom edge to the last
        if (_text.BuildHighlightGeometry(new Point()) is { } boxes)
        {
            Rect all = boxes.Bounds;
            foreach (PathFigure figure in boxes.GetFlattenedPathGeometry().Figures)
            {
                Reach(figure.StartPoint, all);
                foreach (PathSegment segment in figure.Segments)
                    if (segment is PolyLineSegment poly) foreach (Point p in poly.Points) Reach(p, all);
                    else if (segment is LineSegment line) Reach(line.Point, all);
            }
        }
        for (int i = 0; i < rows; i++)
        {
            _masks[i] = new LinearGradientBrush { MappingMode = BrushMappingMode.Absolute, EndPoint = new Point(Math.Max(width, 1), 0) };
            // lit from the start to the boundary, which the two stops in the middle are the sides of
            foreach (double offset in new[] { 0.0, 0, 1, 1 }) _masks[i].GradientStops.Add(new GradientStop(Colors.Black, offset));
        }
        Sweep();
        return new Size(width, rows * LineHeight);
    }

    void Reach(Point p, Rect all)
    {
        if (p.Y < all.Top + 0.5) _rows[0] = Math.Max(_rows[0], p.X);
        if (p.Y > all.Bottom - 0.5) _rows[^1] = Math.Max(_rows[^1], p.X);
    }

    // moves the boundary along the rows: the brushes are already in the picture, so nothing is drawn again
    void Sweep()
    {
        double width = _masks.Length > 0 ? _masks[0].EndPoint.X : 0;
        Color rest = Color.FromArgb((byte)(255 * Math.Clamp(Unsung, 0, 1)), 0, 0, 0);
        // each row is swept from a boundary's width before its start, so it opens with nothing lit
        double at = Math.Clamp(Progress, 0, 1) * (_rows.Sum() + Edge * _rows.Length);
        for (int i = 0; i < _masks.Length; i++)
        {
            double from = Math.Clamp(at, 0, _rows[i] + Edge) - Edge;
            at -= _rows[i] + Edge;
            GradientStopCollection stops = _masks[i].GradientStops;
            stops[1].Offset = Math.Clamp(from / width, 0, 1);
            stops[2].Offset = Math.Clamp((from + Edge) / width, 0, 1);
            stops[2].Color = stops[3].Color = rest;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_text == null) return;
        if (Unsung >= 1)
        {
            dc.DrawText(_text, new Point());
            return;
        }

        for (int i = 0; i < _masks.Length; i++)
        {
            dc.PushClip(new RectangleGeometry(new Rect(0, i * LineHeight, ActualWidth, LineHeight)));
            dc.PushOpacityMask(_masks[i]);
            dc.DrawText(_text, new Point());
            dc.Pop();
            dc.Pop();
        }
    }
}
