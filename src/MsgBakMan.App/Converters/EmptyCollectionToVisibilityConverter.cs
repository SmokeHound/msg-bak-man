using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MsgBakMan.App.Converters;

public sealed class EmptyCollectionToVisibilityConverter : IValueConverter
{
    public Visibility EmptyVisibility { get; set; } = Visibility.Visible;
    public Visibility NonEmptyVisibility { get; set; } = Visibility.Collapsed;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return EmptyVisibility;
        }

        if (value is ICollection c)
        {
            return c.Count == 0 ? EmptyVisibility : NonEmptyVisibility;
        }

        if (value is IEnumerable e)
        {
            foreach (var _ in e)
            {
                return NonEmptyVisibility;
            }
            return EmptyVisibility;
        }

        return NonEmptyVisibility;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
