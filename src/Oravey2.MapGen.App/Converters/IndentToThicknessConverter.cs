using System.Globalization;

namespace Oravey2.MapGen.App.Converters;

public class IndentToThicknessConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var left = value is double d ? d : 0;
        return new Thickness(left + 8, 4, 8, 4);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
