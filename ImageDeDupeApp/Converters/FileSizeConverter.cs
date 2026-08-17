using System;
using System.Windows.Data;

namespace ImageDeDupeApp.Converters
{
    /// <summary>
    /// Converts a file size in bytes to a human-readable string representation (e.g., KB, MB, GB).
    /// </summary>
    public class FileSizeConverter : IValueConverter
    {
        /// <summary>
        /// Converts a long integer byte count to a formatted string.
        /// </summary>
        /// <param name="value">The byte count as a long.</param>
        /// <param name="targetType">The type of the binding target property (String).</param>
        /// <param name="parameter">An optional parameter to customize conversion (unused).</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>A formatted file size string (e.g., "2.4 MB").</returns>
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is long bytes)
            {
                string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
                int counter = 0;
                decimal number = bytes;

                // Divide by 1024 iteratively to determine the correct unit suffix
                while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
                {
                    number /= 1024;
                    counter++;
                }

                // Format with one decimal place and the corresponding suffix
                return $"{number:n1} {suffixes[counter]}";
            }
            return "0 B";
        }

        /// <summary>
        /// ConvertBack is not implemented as file sizes are read-only bindings.
        /// </summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
