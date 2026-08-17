using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace ImageDeDupeApp.Models
{
    /// <summary>
    /// Represents an image file on disk, encapsulating its basic properties, EXIF metadata, GPS, and hashes.
    /// </summary>
    public class ImageFile
    {
        /// <summary>Gets the absolute file path of the image.</summary>
        public string FilePath { get; }

        /// <summary>Gets the filename with extension.</summary>
        public string FileName { get; }

        /// <summary>Gets the size of the file on disk in bytes.</summary>
        public long FileSize { get; }

        /// <summary>Gets the MD5 binary hash of the file (computed on-demand).</summary>
        public string? FileHash { get; private set; }

        /// <summary>Gets the date the image was taken, falling back to the last write time if EXIF is missing.</summary>
        public DateTime? DateTaken { get; private set; }

        /// <summary>Gets a value indicating whether the DateTaken was successfully loaded from EXIF metadata.</summary>
        public bool HasExifDate { get; private set; }

        /// <summary>Gets the GPS Latitude coordinate, if available.</summary>
        public double? Latitude { get; private set; }

        /// <summary>Gets the GPS Longitude coordinate, if available.</summary>
        public double? Longitude { get; private set; }

        /// <summary>Gets a value indicating whether the file contains both latitude and longitude GPS metadata.</summary>
        public bool HasGps => Latitude.HasValue && Longitude.HasValue;

        /// <summary>Gets the camera model used to capture the image, if available.</summary>
        public string? CameraModel { get; private set; }

        /// <summary>Gets or sets the horizontal difference hash (dHash) used for structural comparison.</summary>
        public ulong? DifferenceHash { get; set; }

        /// <summary>Gets or sets the vertical difference hash used for structural comparison.</summary>
        public ulong? VerticalHash { get; set; }

        /// <summary>Gets or sets the 48-byte RGB spatial color signature used for color comparison.</summary>
        public byte[]? ColorSignature { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFile"/> class.
        /// </summary>
        /// <param name="filePath">The absolute path to the image file.</param>
        public ImageFile(string filePath)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            var fi = new FileInfo(filePath);
            FileSize = fi.Exists ? fi.Length : 0;
        }

        /// <summary>
        /// Reads EXIF metadata from the file, extracting capture date, camera model, and GPS coordinates.
        /// Falls back to file system timestamps if metadata extraction fails or is unavailable.
        /// </summary>
        public void LoadMetadata()
        {
            try
            {
                // Parse directories containing EXIF and IPTC metadata blocks using MetadataExtractor
                var directories = ImageMetadataReader.ReadMetadata(FilePath);
                
                // Extract EXIF SubIFD for Date Taken (DateTimeOriginal)
                var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (subIfd != null && subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dt))
                {
                    DateTaken = dt;
                    HasExifDate = true;
                }
                else
                {
                    // Fallback to filesystem's last write time if EXIF original date is missing
                    DateTaken = File.GetLastWriteTime(FilePath);
                    HasExifDate = false;
                }

                // Extract Camera Model details from EXIF IFD0 block
                var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                if (ifd0 != null)
                {
                    CameraModel = ifd0.GetString(ExifDirectoryBase.TagModel);
                }

                // Extract GPS coordinates from standard GPS subblock
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
                // Fallback catch-all for corrupt images, unrecognized formats, or access exceptions
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

        /// <summary>
        /// Lazily computes the MD5 binary hash of the file contents for exact bit-wise matching.
        /// </summary>
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
