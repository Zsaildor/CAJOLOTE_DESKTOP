using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Cajolote.Converters;

public class ProgressToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double progress)
        {
            // Progress goes from 100 down to 0.
            // Last 3 seconds out of 10 means progress <= 30.
            if (progress <= 30)
            {
                // Smoothly interpolate from Purple (#9B5DE5) to Pale Red (#E67E80)
                double t = (30.0 - progress) / 30.0;
                t = Math.Max(0.0, Math.Min(1.0, t));

                Color startColor = (Color)ColorConverter.ConvertFromString("#9B5DE5");
                Color endColor = (Color)ColorConverter.ConvertFromString("#E67E80");

                byte r = (byte)(startColor.R + (endColor.R - startColor.R) * t);
                byte g = (byte)(startColor.G + (endColor.G - startColor.G) * t);
                byte b = (byte)(startColor.B + (endColor.B - startColor.B) * t);

                return new SolidColorBrush(Color.FromRgb(r, g, b));
            }
            else
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9B5DE5"));
            }
        }
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9B5DE5"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
