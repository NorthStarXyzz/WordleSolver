using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace WordleSolver.Converters
{
    public class ColorIndexToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int colorIndex)
            {
                return colorIndex switch
                {
                    0 => new SolidColorBrush(Colors.Gray),      
                    1 => new SolidColorBrush(Colors.Yellow),    
                    2 => new SolidColorBrush(Colors.Green),     
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}