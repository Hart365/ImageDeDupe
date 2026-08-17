using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ImageDeDupeApp.Models
{
    public class DuplicateImage : INotifyPropertyChanged
    {
        private bool _isSelected = true; // Default to checked for sweeping
        private string _status = "Ready to move";

        public ImageFile Image { get; }
        public double SimilarityPercentage { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AccessibilityName));
                }
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AccessibilityName));
                }
            }
        }

        public string AccessibilityName
        {
            get
            {
                string sizeStr = FormatBytes(Image.FileSize);
                string dateStr = Image.DateTaken.HasValue ? Image.DateTaken.Value.ToString("g") : "unknown";
                string gpsStr = Image.HasGps ? "has GPS location" : "no GPS location";
                string checkStr = IsSelected ? "selected" : "not selected";
                return $"Duplicate image: {Image.FileName}, {SimilarityPercentage:F0} percent similar. Size {sizeStr}, taken {dateStr}, {gpsStr}. Status is {Status}. Action is {checkStr}.";
            }
        }

        public DuplicateImage(ImageFile image, double similarityPercentage)
        {
            Image = image;
            SimilarityPercentage = similarityPercentage;
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DuplicateGroup : INotifyPropertyChanged
    {
        private bool _isGroupSelected = true;

        public ImageFile PrimaryImage { get; }
        public ObservableCollection<DuplicateImage> Duplicates { get; } = new();

        public bool IsGroupSelected
        {
            get => _isGroupSelected;
            set
            {
                if (_isGroupSelected != value)
                {
                    _isGroupSelected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AccessibilityName));
                    foreach (var dup in Duplicates)
                    {
                        dup.IsSelected = value;
                    }
                }
            }
        }

        public string AccessibilityName
        {
            get
            {
                string sizeStr = FormatBytes(PrimaryImage.FileSize);
                string dateStr = PrimaryImage.DateTaken.HasValue ? PrimaryImage.DateTaken.Value.ToString("g") : "unknown";
                string gpsStr = PrimaryImage.HasGps ? "has GPS location" : "no GPS location";
                return $"Original primary image: {PrimaryImage.FileName}. Size {sizeStr}, taken {dateStr}, {gpsStr}. This group has {Duplicates.Count} potential duplicates.";
            }
        }

        public DuplicateGroup(ImageFile primaryImage)
        {
            PrimaryImage = primaryImage;
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
