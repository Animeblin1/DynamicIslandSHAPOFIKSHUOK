using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DynamicIsland;

/// <summary>
/// A file on the shelf: its picture in a rounded square with its name under it. Under the pointer the square swells
/// a little and a cross comes up on its corner to take the file off.
/// </summary>
sealed class ShelfTile : Grid
{
    public const double Wide = 64;    // the tile, its name included
    const double Square = 56, Radius = 13;
    const double IconSize = 40;       // an icon sits in the square; a photo fills it
    const double Swell = 1.06;

    readonly Border _frame;
    readonly ScaleTransform _swell = new(1, 1);
    readonly Border _cross;

    public ShelfTile(Shelf.Item item)
    {
        Item = item;
        Width = Wide;
        Background = Brushes.Transparent;
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(Square) });
        RowDefinitions.Add(new RowDefinition());
        RenderTransformOrigin = new Point(0.5, 0.5);
        RenderTransform = new ScaleTransform(1, 1);

        _frame = new Border
        {
            Width = Square,
            Height = Square,
            CornerRadius = new CornerRadius(Radius),
            Background = new SolidColorBrush(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF)),
            // the photo is cut to the square's round corners
            Clip = new RectangleGeometry(new Rect(0, 0, Square, Square), Radius, Radius),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = _swell,
        };
        RenderOptions.SetBitmapScalingMode(_frame, BitmapScalingMode.HighQuality);
        Children.Add(_frame);

        var name = new TextBlock
        {
            Text = item.Name,
            FontSize = 10.5,
            Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 5, 0, 0),
        };
        SetRow(name, 1);
        Children.Add(name);

        // on the square's top right corner, ringed in the island's black so it stands off whatever the picture is
        _cross = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromRgb(0x48, 0x48, 0x4A)),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -6, -2, 0),
            Opacity = 0,
            Cursor = Cursors.Hand,
            Child = new Icon { Kind = Glyph.Cross, Width = 10, Height = 10 },
        };
        // a press on it is its own, not the start of a drag
        _cross.MouseLeftButtonDown += (_, e) => e.Handled = true;
        _cross.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Removed?.Invoke(this);
        };
        Children.Add(_cross);

        Cursor = Cursors.Hand;
        MouseEnter += (_, _) => Hover(true);
        MouseLeave += (_, _) => Hover(false);
        Show();
    }

    public Shelf.Item Item { get; }

    /// <summary>The cross was clicked.</summary>
    public event Action<ShelfTile>? Removed;

    /// <summary>Puts the picture in, once the shelf has it.</summary>
    public void Show()
    {
        if (Item.Picture is not { } picture) return;
        // a photo is the fill of a square of its own, cut to it with the same corners: an image filling the square
        // would be laid out larger than it and spill past its top
        FrameworkElement shown = Item.Photo
            ? new Border
            {
                CornerRadius = new CornerRadius(Radius),
                Background = new ImageBrush(picture) { Stretch = Stretch.UniformToFill },
            }
            : new Image { Source = picture, Stretch = Stretch.Uniform, Width = IconSize, Height = IconSize };
        // it comes in over the empty square instead of popping up in it
        shown.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Ms(220)));
        _frame.Child = shown;
    }

    void Hover(bool on)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var size = new DoubleAnimation(on ? Swell : 1, Ms(on ? 160 : 260)) { EasingFunction = ease };
        _swell.BeginAnimation(ScaleTransform.ScaleXProperty, size);
        _swell.BeginAnimation(ScaleTransform.ScaleYProperty, size);
        _cross.BeginAnimation(OpacityProperty, new DoubleAnimation(on ? 1 : 0, Ms(on ? 120 : 220)));
    }

    static Duration Ms(double ms) => TimeSpan.FromMilliseconds(ms);
}
