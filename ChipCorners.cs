using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DynamicIsland;

/// <summary>
/// Corners of a chip from its height: 14 px, or half the height on a chip too low for that — a larger radius is
/// squeezed to fit the height alone, and the ends come out pointed instead of round.
/// </summary>
public sealed class ChipCorners : IValueConverter
{
    const double Most = 14;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        new CornerRadius(Math.Min(Most, (double)value / 2));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
