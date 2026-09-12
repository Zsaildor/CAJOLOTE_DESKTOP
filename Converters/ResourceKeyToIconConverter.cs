using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Cajolote.Converters;

public class ResourceKeyToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string resourceKey && !string.IsNullOrEmpty(resourceKey))
        {
            try
            {
                var resource = Application.Current.TryFindResource(resourceKey);
                if (resource is Drawing drawing)
                {
                    return new DrawingImage(drawing);
                }
                if (resource is DrawingImage drawingImage)
                {
                    return drawingImage;
                }
                if (resource is ImageSource imageSource)
                {
                    return imageSource;
                }
            }
            catch
            {
                // Retorna null si no se encuentra o hay error
            }
        }
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
