using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace Cajolote.Converters;

public class IconKeyToFriendlyNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key && !string.IsNullOrEmpty(key))
        {
            if (key.StartsWith("Icon_"))
            {
                key = key.Substring(5);
            }
            
            // Inserta un espacio antes de cada letra mayúscula que no sea el inicio de una palabra
            // Ej: "QuesoOaxaca" -> "Queso Oaxaca"
            string friendlyName = Regex.Replace(key, @"(\B[A-Z])", " $1");
            return friendlyName;
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
