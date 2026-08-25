using System.Globalization;
using System.Windows.Data;

namespace MyAiAssistant.Converters;

public class ScreenCenterConverter : IValueConverter
{
    public static readonly ScreenCenterConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double screenWidth && parameter is string offsetStr && double.TryParse(offsetStr, out var offset))
        {
            return screenWidth / 2 + offset;
        }
        if (value is double w)
            return w / 2;
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
