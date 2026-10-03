using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace DynamicIsland;

/// <summary>A changing number: only the digits that differ move — the old one drops away into a blur, the new one settles in sharp.</summary>
public sealed class Digits : ContentControl
{
    readonly StackPanel _row = new() { Orientation = Orientation.Horizontal };
    readonly List<TextBlock?> _glyphs = new(); // what each cell of the row currently reads
    string _text = "";

    public Digits()
    {
        Focusable = false;
        IsTabStop = false;
        // every digit is its own cell: rounding each one to whole pixels would spread them unevenly
        UseLayoutRounding = false;
        Content = _row;
    }

    /// <summary>New digits arrive from above and the old ones fall away, as a countdown reads; false turns it round.
    /// Left unset, they follow the number: up as it grows, down as it shrinks.</summary>
    public bool? Down { get; set; }

    bool _down = true; // the way the change under way rolls

    public string Text
    {
        get => _text;
        set
        {
            value ??= "";
            if (value == _text) return;
            _down = Down ?? Shrinks(_text, value) ?? _down;
            _text = value;
            // off screen there is nobody to animate for
            Show(value, IsVisible);
        }
    }

    /// <summary>Whether the number read in <paramref name="next"/> is smaller than the one before; null when either has none or they are equal.
    /// Only the digits count, so "1:05" against "0:59" or "-3:20" against "-3:19" compare as they read.</summary>
    static bool? Shrinks(string was, string next)
    {
        if (!was.Any(char.IsAsciiDigit) || !next.Any(char.IsAsciiDigit)) return null;
        string a = string.Concat(was.Where(char.IsAsciiDigit)).TrimStart('0');
        string b = string.Concat(next.Where(char.IsAsciiDigit)).TrimStart('0');
        // longer is larger, the same length reads left to right: no number is too long for this
        int order = a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
        return order == 0 ? null : order > 0;
    }

    double Travel => Math.Round(FontSize * 0.5);
    double Blur => Math.Clamp(FontSize * 0.4, 4, 12);

    void Show(string text, bool animate)
    {
        // cells are matched from the right: the seconds stay where they are when the number gets shorter
        while (_glyphs.Count > text.Length)
        {
            _row.Children.RemoveAt(0);
            _glyphs.RemoveAt(0);
        }
        while (_glyphs.Count < text.Length)
        {
            _row.Children.Insert(0, new Grid());
            _glyphs.Insert(0, null);
        }

        for (int i = 0; i < text.Length; i++)
        {
            string symbol = text[i].ToString();
            TextBlock? old = _glyphs[i];
            if (old?.Text == symbol) continue;

            var cell = (Grid)_row.Children[i];
            var next = new TextBlock { Text = symbol, RenderTransform = new TranslateTransform() };
            // equal-width digits, or the cells would shuffle sideways with every change
            Typography.SetNumeralAlignment(next, FontNumeralAlignment.Tabular);
            _glyphs[i] = next;

            if (!animate) cell.Children.Clear();
            cell.Children.Add(next);
            if (!animate) continue;

            if (old != null) Leave(cell, old);
            Enter(next);
        }
    }

    void Leave(Grid cell, TextBlock glyph)
    {
        var blur = new BlurEffect { Radius = 0 };
        glyph.Effect = blur;
        blur.BeginAnimation(BlurEffect.RadiusProperty, new DoubleAnimation(Blur, Ms(220)));
        ((TranslateTransform)glyph.RenderTransform).BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(_down ? Travel : -Travel, Ms(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } });

        var fade = new DoubleAnimation(0, Ms(200));
        fade.Completed += (_, _) => cell.Children.Remove(glyph);
        glyph.BeginAnimation(OpacityProperty, fade);
    }

    void Enter(TextBlock glyph)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var blur = new BlurEffect { Radius = Blur };
        glyph.Effect = blur;
        var sharpen = new DoubleAnimation(0, Ms(320)) { EasingFunction = ease };
        sharpen.Completed += (_, _) =>
        {
            // drop the effect so the digit is rendered crisp again
            if (ReferenceEquals(glyph.Effect, blur)) glyph.Effect = null;
        };
        blur.BeginAnimation(BlurEffect.RadiusProperty, sharpen);
        ((TranslateTransform)glyph.RenderTransform).BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(_down ? -Travel : Travel, 0, Ms(380)) { EasingFunction = ease });
        glyph.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Ms(260)));
    }

    static Duration Ms(double ms) => TimeSpan.FromMilliseconds(ms);
}
