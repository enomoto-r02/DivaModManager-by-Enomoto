using DivaModManager.Common.Helpers;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace DivaModManager.Common.Converters
{
    public class ImageCacheConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Uri uri && uri != null)
            {
                var cacheHours = 24;
                if (parameter is string param && int.TryParse(param, out var hours))
                    cacheHours = hours;
                var cached = ImageCacheManager.GetCachedBitmap(uri, cacheHours);
                if (cached != null)
                    return cached;
                // キャッシュミス: Uri をそのまま返し（WPF が非同期読み込み）、バックグラウンドでキャッシュ保存
                _ = ImageCacheManager.PreCacheAsync(uri, cacheHours);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
