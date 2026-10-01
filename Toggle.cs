using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DynamicIsland;

/// <summary>Switch of a menu row: the knob slides across while the track turns green.</summary>
public sealed class Toggle : FrameworkElement
{
    const double Inset = 2; // between the knob and the edge of the track

    static readonly Color Off = Color.FromRgb(0x39, 0x39, 0x3D);
    static readonly Color On = Color.FromRgb(0x30, 0xD1, 0x58);

    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(Toggle),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>How far the knob has got, 0 (off) → 1 (on).</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public void Set(bool on, bool animate) =>
        BeginAnimation(ProgressProperty, new DoubleAnimation(on ? 1 : 0, TimeSpan.FromMilliseconds(animate ? 220 : 0))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double share = Math.Clamp(Progress, 0, 1);
        var track = new SolidColorBrush(Color.FromRgb(Mix(Off.R, On.R, share), Mix(Off.G, On.G, share), Mix(Off.B, On.B, share)));
        dc.DrawRoundedRectangle(track, null, new Rect(0, 0, w, h), h / 2, h / 2);
        dc.DrawEllipse(Brushes.White, null, new Point(h / 2 + (w - h) * share, h / 2), h / 2 - Inset, h / 2 - Inset);
    }

    static byte Mix(byte from, byte to, double share) => (byte)Math.Round(from + (to - from) * share);
}
