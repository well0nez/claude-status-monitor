using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ClaudeStatusMonitor.Converters
{
    public class UtilizationToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double utilization)
            {
                if (utilization <= 60)
                {
                    return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Grün
                }
                else if (utilization <= 85)
                {
                    return new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
                }
                else
                {
                    return new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Rot
                }
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}