using System;
using System.Globalization;
using System.Windows.Data;

namespace MsgBakMan.App.Converters;

public sealed class WidthToColumnsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double width || double.IsNaN(width) || double.IsInfinity(width))
        {
            return 1;
        }

        var minWidthForTwoColumns = 780.0;
        var minWidthForThreeColumns = 1200.0;

        // Parameter supports:
        // - "780" -> 1/2 columns
        // - "780,1200" -> 1/2/3 columns
        if (parameter is not null)
        {
            var s = parameter.ToString();
            if (!string.IsNullOrWhiteSpace(s))
            {
                var parts = s.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1 &&
                    double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed2))
                {
                    minWidthForTwoColumns = parsed2;
                }

                if (parts.Length >= 2 &&
                    double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed3))
                {
                    minWidthForThreeColumns = parsed3;
                }
            }
        }

        if (width >= minWidthForThreeColumns)
        {
            return 3;
        }

        return width >= minWidthForTwoColumns ? 2 : 1;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
