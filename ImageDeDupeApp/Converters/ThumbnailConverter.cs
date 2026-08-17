using System;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ImageDeDupeApp.Converters
{
    public class ThumbnailConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string filePath && File.Exists(filePath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
                    bitmap.DecodePixelWidth = 120; // Decode as small thumbnail to save memory
                    bitmap.UriSource = new Uri(filePath);
                    bitmap.EndInit();
                    bitmap.Freeze(); // Freezes the object to make it thread-safe and optimize performance
                    return bitmap;
                }
                catch (Exception)
                {
                    // Return null on failure; UI can show a fallback icon or text
                    return null;
                }
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
