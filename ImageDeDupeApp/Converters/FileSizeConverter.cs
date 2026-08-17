using System;
using System.Windows.Data;

namespace ImageDeDupeApp.Converters
{
    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is long bytes)
            {
                string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
                int counter = 0;
                decimal number = bytes;
                while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
                {
                    number /= 1024;
                    counter++;
                }
                return $"{number:n1} {suffixes[counter]}";
            }
            return "0 B";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
