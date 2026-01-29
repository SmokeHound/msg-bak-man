using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MsgBakMan.App.Converters;

public sealed class ZeroCountToVisibilityConverter : IValueConverter
{
    public Visibility ZeroVisibility { get; set; } = Visibility.Visible;
    public Visibility NonZeroVisibility { get; set; } = Visibility.Collapsed;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return ZeroVisibility;
        }

        if (value is int i)
        {
            return i == 0 ? ZeroVisibility : NonZeroVisibility;
        }

        if (value is long l)
        {
            return l == 0 ? ZeroVisibility : NonZeroVisibility;
        }

        if (value is short s)
        {
            return s == 0 ? ZeroVisibility : NonZeroVisibility;
        }

        if (value is byte b)
        {
            return b == 0 ? ZeroVisibility : NonZeroVisibility;
        }

        // Unknown type: assume non-zero.
        return NonZeroVisibility;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
