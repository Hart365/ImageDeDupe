using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace ImageDeDupeApp.Models
{
    public class ImageFile
    {
        public string FilePath { get; }
        public string FileName { get; }
        public long FileSize { get; }
        public string? FileHash { get; private set; }
        public DateTime? DateTaken { get; private set; }
        public bool HasExifDate { get; private set; }
        public double? Latitude { get; private set; }
        public double? Longitude { get; private set; }
        public bool HasGps => Latitude.HasValue && Longitude.HasValue;
        public string? CameraModel { get; private set; }
        public ulong? DifferenceHash { get; set; }
        public ulong? VerticalHash { get; set; }
        public byte[]? ColorSignature { get; set; }

        public ImageFile(string filePath)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            var fi = new FileInfo(filePath);
            FileSize = fi.Exists ? fi.Length : 0;
        }

        public void LoadMetadata()
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(FilePath);
                
                // Extract EXIF SubIFD for Date Taken
                var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (subIfd != null && subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dt))
                {
                    DateTaken = dt;
                    HasExifDate = true;
                }
                else
                {
                    // Fallback to file write time
                    DateTaken = File.GetLastWriteTime(FilePath);
                    HasExifDate = false;
                }

                // Extract Camera Model
                var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                if (ifd0 != null)
                {
                    CameraModel = ifd0.GetString(ExifDirectoryBase.TagModel);
                }

                // Extract GPS coordinates
                var gpsDir = directories.OfType<GpsDirectory>().FirstOrDefault();
                if (gpsDir != null)
                {
                    var location = gpsDir.GetGeoLocation();
                    if (location != null)
                    {
                        Latitude = location.Value.Latitude;
                        Longitude = location.Value.Longitude;
                    }
                }
            }
            catch (Exception)
            {
                // Fallback for files with corrupt or no EXIF
                try
                {
                    DateTaken = File.GetLastWriteTime(FilePath);
                }
                catch
                {
                    DateTaken = DateTime.MinValue;
                }
                HasExifDate = false;
            }
        }

        public void LoadFileHash()
        {
            if (FileHash != null) return;
            try
            {
                using (var md5 = MD5.Create())
                using (var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var hashBytes = md5.ComputeHash(stream);
                    FileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception)
            {
                FileHash = string.Empty;
            }
        }
    }
}
