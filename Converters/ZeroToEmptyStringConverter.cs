using System;
using System.Globalization;
using System.Windows.Data;

namespace Cajolote.Converters;

public class ZeroToEmptyStringConverter : IValueConverter
{
    private System.Windows.Controls.TextBox? GetFocusedTextBox()
    {
        var focused = System.Windows.Input.Keyboard.FocusedElement as System.Windows.DependencyObject;
        while (focused != null)
        {
            if (focused is System.Windows.Controls.TextBox textBox)
                return textBox;

            System.Windows.DependencyObject? parent = null;
            if (focused is System.Windows.Media.Visual || focused is System.Windows.Media.Media3D.Visual3D)
            {
                try { parent = System.Windows.Media.VisualTreeHelper.GetParent(focused); }
                catch { }
            }
            
            if (parent == null)
            {
                parent = System.Windows.LogicalTreeHelper.GetParent(focused);
            }

            if (parent == null && focused is System.Windows.FrameworkElement fe)
            {
                parent = fe.Parent ?? fe.TemplatedParent;
            }

            if (parent == focused) break;
            focused = parent;
        }
        return null;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Try to preserve typing (like dots and trailing zeros) if the user is actively editing
        var textBox = GetFocusedTextBox();
        if (textBox != null)
        {
            string currentText = textBox.Text;
            string cleaned = currentText.Trim();
            var decSep = culture.NumberFormat.NumberDecimalSeparator;
            cleaned = cleaned.Replace(",", decSep).Replace(".", decSep);

            // Permitir prefijos válidos que no son "números" completos aún
            bool isPartialNumeric = string.IsNullOrEmpty(cleaned) || 
                                    cleaned == "-" || 
                                    cleaned == decSep || 
                                    cleaned == "-" + decSep ||
                                    cleaned == "0" + decSep ||
                                    cleaned == "-0" + decSep;

            if (isPartialNumeric)
            {
                return currentText;
            }

            if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, culture, out decimal parsedCurrent))
            {
                decimal targetVal = 0m;
                bool isNumeric = false;

                if (value is decimal decVal) { targetVal = decVal; isNumeric = true; }
                else if (value is double dblVal) { targetVal = (decimal)dblVal; isNumeric = true; }
                else if (value is float fltVal) { targetVal = (decimal)fltVal; isNumeric = true; }
                else if (value is int intVal) { targetVal = (decimal)intVal; isNumeric = true; }

                if (isNumeric && targetVal == parsedCurrent)
                {
                    return currentText;
                }
            }
        }

        if (value is decimal decValue)
        {
            return decValue == 0 ? string.Empty : decValue.ToString("G", culture);
        }
        if (value is double dblValue)
        {
            return dblValue == 0 ? string.Empty : dblValue.ToString("G", culture);
        }
        if (value is float fltValue)
        {
            return fltValue == 0 ? string.Empty : fltValue.ToString("G", culture);
        }
        if (value is int intValue)
        {
            return intValue == 0 ? string.Empty : intValue.ToString(culture);
        }
        return value ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? stringValue = value as string;
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            if (targetType == typeof(decimal)) return 0m;
            if (targetType == typeof(double)) return 0.0;
            if (targetType == typeof(float)) return 0.0f;
            if (targetType == typeof(int)) return 0;
            return 0m;
        }

        string cleaned = stringValue.Trim();
        
        // Handle dot/comma replacement for decimals
        var decSep = culture.NumberFormat.NumberDecimalSeparator;
        cleaned = cleaned.Replace(",", decSep).Replace(".", decSep);

        if (targetType == typeof(decimal))
        {
            if (decimal.TryParse(cleaned, NumberStyles.Any, culture, out decimal result))
            {
                return result;
            }
            return 0m;
        }
        if (targetType == typeof(double))
        {
            if (double.TryParse(cleaned, NumberStyles.Any, culture, out double result))
            {
                return result;
            }
            return 0.0;
        }
        if (targetType == typeof(float))
        {
            if (float.TryParse(cleaned, NumberStyles.Any, culture, out float result))
            {
                return result;
            }
            return 0.0f;
        }
        if (targetType == typeof(int))
        {
            if (int.TryParse(cleaned, NumberStyles.Any, culture, out int result))
            {
                return result;
            }
            return 0;
        }

        return value ?? 0m;
    }
}
