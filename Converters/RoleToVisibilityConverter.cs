using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MyAiAssistant.Converters;

public class RoleToVisibilityConverter : IValueConverter
{
    public static readonly RoleToVisibilityConverter User = new("user");
    public static readonly RoleToVisibilityConverter AI = new("assistant");

    private readonly string _role;

    public RoleToVisibilityConverter() { _role = "user"; }
    public RoleToVisibilityConverter(string role) { _role = role; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString()?.Equals(_role, StringComparison.OrdinalIgnoreCase) == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
