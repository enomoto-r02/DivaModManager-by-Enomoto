using System;
using System.Windows.Data;
using System.Windows.Media;

namespace DivaModManager.Common.Converters
{
    public class CategoryColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (string)value switch
            {
                "BugFix" => new SolidColorBrush(Color.FromRgb(255, 78, 78)),
                "Overhaul" => new SolidColorBrush(Color.FromRgb(255, 78, 78)),
                "Addition" => new SolidColorBrush(Color.FromRgb(108, 177, 255)),
                "Feature" => new SolidColorBrush(Color.FromRgb(108, 177, 255)),
                "Tweak" => new SolidColorBrush(Color.FromRgb(255, 94, 157)),
                "Improvement" => new SolidColorBrush(Color.FromRgb(255, 94, 157)),
                "Optimization" => new SolidColorBrush(Color.FromRgb(255, 94, 157)),
                "Adjustment" => new SolidColorBrush(Color.FromRgb(110, 255, 108)),
                "Suggestion" => new SolidColorBrush(Color.FromRgb(110, 255, 108)),
                "Ammendment" => new SolidColorBrush(Color.FromRgb(110, 255, 108)),
                "Removal" => new SolidColorBrush(Color.FromRgb(153, 153, 153)),
                "Refactor" => new SolidColorBrush(Color.FromRgb(153, 153, 153)),
                _ => new SolidColorBrush(Color.FromRgb(0, 0, 0)),
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
