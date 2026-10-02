using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DynamicIsland;

/// <summary>The rows of a menu with one highlight between them: it runs to the row under the pointer, stretching
/// on the way, instead of every row lighting up by itself — two can never be lit at once, however fast the pointer
/// crosses them.</summary>
public sealed class RowList : StackPanel
{
    const double Radius = 12;
    const double Hover = 0.12, Press = 0.21; // how white the highlight is
    const double SquishX = 5, SquishY = 2;   // px it pulls in from each side under a press
    const double Pop = 0.6;                  // share of that squish it starts from when it appears

    // drawn once and moved afterwards: changing these does not lay the rows out again
    readonly RectangleGeometry _shape = new() { RadiusX = Radius, RadiusY = Radius };
    readonly SolidColorBrush _fill = new(Colors.White) { Opacity = 0 };

    // the two edges run on their own: the one ahead is quick, the one behind is dragged along
    readonly Spring _top = new(0), _bottom = new(0);
    readonly Spring _shown = new(0), _squish = new(0);
    bool _lit, _pressed;
    bool _running;
    long _last;

    public RowList()
    {
        // the buttons mark what they take as handled: the list wants to hear of it anyway
        AddHandler(Mouse.MouseMoveEvent, new MouseEventHandler((_, _) => Sync()), true);
        AddHandler(Mouse.MouseDownEvent, new MouseButtonEventHandler((_, _) => Sync()), true);
        AddHandler(Mouse.MouseUpEvent, new MouseButtonEventHandler((_, _) => Sync()), true);
        AddHandler(Mouse.LostMouseCaptureEvent, new MouseEventHandler((_, _) => Sync()), true);
        MouseEnter += (_, _) => Sync();
        MouseLeave += (_, _) => Sync();
        // put away with its page, it comes back unlit
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) Rest();
        };
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawGeometry(_fill, null, _shape);
    }

    void Sync()
    {
        ButtonBase? row = null;
        foreach (UIElement child in InternalChildren)
            if (child is ButtonBase { IsMouseOver: true } button)
            {
                row = button;
                break;
            }

        if (row != null)
        {
            double top = VisualTreeHelper.GetOffset(row).Y, bottom = top + row.RenderSize.Height;
            if (_shown.Value < 0.05)
            {
                // nothing to run from: it comes up where the pointer is, a little small, and springs to size
                _top.Value = _top.Target = top;
                _bottom.Value = _bottom.Target = bottom;
                _top.Velocity = _bottom.Velocity = 0;
                _squish.Value = Pop;
            }
            else if (top != _top.Target)
            {
                bool down = top > _top.Target;
                (down ? _bottom : _top).Tune(560, 36);
                (down ? _top : _bottom).Tune(230, 26);
            }
            _top.Target = top;
            _bottom.Target = bottom;
        }

        bool lit = row != null, pressed = row is { IsPressed: true };
        // quick to come, slow to go
        if (lit != _lit) _shown.Tune(lit ? 420 : 90, lit ? 41 : 19);
        // pressed in firmly, let go with a bounce
        if (pressed != _pressed) _squish.Tune(pressed ? 700 : 380, pressed ? 44 : 16);
        _lit = lit;
        _pressed = pressed;
        _shown.Target = lit ? 1 : 0;
        _squish.Target = pressed ? 1 : 0;

        if (_running) return;
        _running = true;
        _last = Stopwatch.GetTimestamp();
        CompositionTarget.Rendering += OnFrame;
    }

    void Rest()
    {
        _lit = _pressed = false;
        _shown.Value = _shown.Target = _shown.Velocity = 0;
        _squish.Value = _squish.Target = _squish.Velocity = 0;
        Apply();
    }

    void OnFrame(object? sender, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        double dt = Math.Min(Stopwatch.GetElapsedTime(_last, now).TotalSeconds, 0.05);
        _last = now;
        if (dt <= 0) return;

        bool moving = _top.Advance(dt);
        moving |= _bottom.Advance(dt);
        moving |= _shown.Advance(dt);
        moving |= _squish.Advance(dt);
        Apply();

        if (!moving)
        {
            CompositionTarget.Rendering -= OnFrame;
            _running = false;
        }
    }

    void Apply()
    {
        // let go, the squish swings past zero: the highlight is a touch larger than its row for a moment
        double x = SquishX * _squish.Value, y = SquishY * _squish.Value;
        _shape.Rect = new Rect(x, _top.Value + y,
            Math.Max(ActualWidth - x * 2, 0), Math.Max(_bottom.Value - _top.Value - y * 2, 0));
        _fill.Opacity = Math.Clamp(_shown.Value, 0, 1) * (Hover + (Press - Hover) * Math.Clamp(_squish.Value, 0, 1));
    }
}
