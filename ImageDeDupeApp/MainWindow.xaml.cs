using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using ImageDeDupeApp.Models;
using ImageDeDupeApp.Services;
using System.Windows.Media.Imaging;

namespace ImageDeDupeApp
{
    public partial class MainWindow : Window
    {
        private readonly ImageScanner _scanner = new();
        private ObservableCollection<DuplicateGroup> _duplicateGroups = new();
        private CancellationTokenSource? _cts;
        private bool _isScanning = false;
        private string _selectedFolderPath = string.Empty;
        private double _lastPreviewWidth = 320;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "Select Folder to Scan for Duplicate Images",
                    Multiselect = false
                };

                if (dialog.ShowDialog(this) == true)
                {
                    _selectedFolderPath = dialog.FolderName;
                    TxtFolderPath.Text = _selectedFolderPath;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error selecting folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            if (_isScanning)
            {
                // Cancel current scan
                _cts?.Cancel();
                return;
            }

            // Validation
            if (string.IsNullOrWhiteSpace(_selectedFolderPath) || !Directory.Exists(_selectedFolderPath))
            {
                MessageBox.Show(this, "Please select a valid folder to scan.", "Invalid Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool compareVisual = ChkCompareVisual.IsChecked == true;
            bool compareHash = ChkCompareFileContents.IsChecked == true;
            bool compareDate = ChkCompareDateTime.IsChecked == true;
            bool compareLocation = ChkCompareLocation.IsChecked == true;
            bool compareFilename = ChkCompareFilename.IsChecked == true;
            bool compareSize = ChkCompareFileSize.IsChecked == true;

            bool matchAll = RadMatchAll.IsChecked == true;

            if (!compareVisual && !compareHash && !compareDate && !compareLocation && !compareFilename && !compareSize)
            {
                MessageBox.Show(this, "Please select at least one comparison criterion.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // UI Setup for scanning
            _isScanning = true;
            BtnScan.Content = "Cancel";
            GridProgress.Visibility = Visibility.Visible;
            LstDuplicateGroups.Visibility = Visibility.Collapsed;
            TxtNoResults.Visibility = Visibility.Collapsed;
            BtnSweepSelected.IsEnabled = false;
            BtnSweepAll.IsEnabled = false;
            TxtSummary.Text = string.Empty;
            _duplicateGroups.Clear();

            var options = new ScanOptions
            {
                CompareVisual = compareVisual,
                CompareFileContents = compareHash,
                CompareDateTime = compareDate,
                CompareLocation = compareLocation,
                CompareFilename = compareFilename,
                CompareFileSize = compareSize,
                SimilarityThreshold = SldThreshold.Value,
                MatchAllCriteria = matchAll
            };

            _cts = new CancellationTokenSource();
            var progress = new Progress<(int current, int total, string currentFile)>(UpdateProgress);

            try
            {
                var results = await _scanner.ScanDirectoryAsync(
                    _selectedFolderPath,
                    ChkIncludeSubfolders.IsChecked == true,
                    options,
                    progress,
                    _cts.Token
                );

                DisplayResults(results);
            }
            catch (OperationCanceledException)
            {
                TxtStatus.Text = "Scan cancelled.";
                TxtSummary.Text = "Scan cancelled by user.";
                TxtNoResults.Text = "Scan was cancelled. Choose a folder and try again.";
                TxtNoResults.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"An error occurred during scanning: {ex.Message}", "Scan Error", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "Scan failed.";
                TxtSummary.Text = "Scan failed with errors.";
                TxtNoResults.Text = "An error occurred. Please try scanning again.";
                TxtNoResults.Visibility = Visibility.Visible;
            }
            finally
            {
                _isScanning = false;
                BtnScan.Content = "Start Scan";
                GridProgress.Visibility = Visibility.Collapsed;
                _cts.Dispose();
                _cts = null;
            }
        }

        private void UpdateProgress((int current, int total, string currentFile) state)
        {
            TxtStatus.Text = state.currentFile;
            if (state.total > 0)
            {
                PrgScan.Maximum = state.total;
                PrgScan.Value = state.current;
            }
        }

        private void DisplayResults(List<DuplicateGroup> groups)
        {
            if (groups.Count == 0)
            {
                TxtNoResults.Text = "No duplicates found with the selected criteria.";
                TxtNoResults.Visibility = Visibility.Visible;
                LstDuplicateGroups.Visibility = Visibility.Collapsed;
                TxtSummary.Text = "0 duplicate groups found.";
                return;
            }

            _duplicateGroups = new ObservableCollection<DuplicateGroup>(groups);
            LstDuplicateGroups.ItemsSource = _duplicateGroups;
            LstDuplicateGroups.Visibility = Visibility.Visible;
            TxtNoResults.Visibility = Visibility.Collapsed;

            int totalDuplicates = _duplicateGroups.Sum(g => g.Duplicates.Count);
            TxtSummary.Text = $"Found {_duplicateGroups.Count} original images with {totalDuplicates} duplicates.";
            
            BtnSweepSelected.IsEnabled = true;
            BtnSweepAll.IsEnabled = true;
        }

        private void BtnSweepSelected_Click(object sender, RoutedEventArgs e)
        {
            PerformSweep(selectedOnly: true);
        }

        private void BtnSweepAll_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(this, 
                "Are you sure you want to move ALL duplicates to the 'Duplicates' folder? (Only the original of each group will remain).", 
                "Confirm Move All", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                PerformSweep(selectedOnly: false);
            }
        }

        private void PerformSweep(bool selectedOnly)
        {
            if (string.IsNullOrEmpty(_selectedFolderPath)) return;

            string duplicatesDir = Path.Combine(_selectedFolderPath, "Duplicates");
            int moveCount = 0;
            int errorCount = 0;

            try
            {
                if (!Directory.Exists(duplicatesDir))
                {
                    Directory.CreateDirectory(duplicatesDir);
                }

                foreach (var group in _duplicateGroups)
                {
                    foreach (var dup in group.Duplicates)
                    {
                        // Skip if already moved or if we only want selected and it isn't selected
                        if (dup.Status == "Moved" || (selectedOnly && !dup.IsSelected))
                        {
                            continue;
                        }

                        try
                        {
                            string srcPath = dup.Image.FilePath;
                            if (!File.Exists(srcPath))
                            {
                                dup.Status = "File not found";
                                errorCount++;
                                continue;
                            }

                            // Generate unique destination path to prevent overwrites
                            string destFileName = dup.Image.FileName;
                            string destPath = Path.Combine(duplicatesDir, destFileName);
                            if (File.Exists(destPath))
                            {
                                string nameWithoutExt = Path.GetFileNameWithoutExtension(destFileName);
                                string ext = Path.GetExtension(destFileName);
                                int count = 1;
                                do
                                {
                                    destPath = Path.Combine(duplicatesDir, $"{nameWithoutExt}_duplicate{count}{ext}");
                                    count++;
                                } while (File.Exists(destPath));
                            }

                            // Perform move operation
                            File.Move(srcPath, destPath);
                            
                            dup.Status = "Moved";
                            dup.IsSelected = false; // Deselect once moved
                            moveCount++;
                        }
                        catch (Exception ex)
                        {
                            dup.Status = $"Error: {ex.Message}";
                            errorCount++;
                        }
                    }
                }

                // Refresh summary
                int remainingDuplicates = _duplicateGroups.Sum(g => g.Duplicates.Count(d => d.Status != "Moved"));
                int remainingGroups = _duplicateGroups.Count(g => g.Duplicates.Any(d => d.Status != "Moved"));

                string summary = $"Operation complete. Moved {moveCount} files to Duplicates folder.";
                if (errorCount > 0)
                {
                    summary += $" Errors: {errorCount} failed.";
                }
                
                MessageBox.Show(this, summary, "Sweep Complete", MessageBoxButton.OK, 
                    errorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                TxtSummary.Text = $"Swept {moveCount} files. {remainingGroups} groups containing {remainingDuplicates} duplicates remain.";

                // If all duplicates were moved, disable action buttons
                if (remainingDuplicates == 0)
                {
                    BtnSweepSelected.IsEnabled = false;
                    BtnSweepAll.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to complete sweep operation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ListViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true; // Prevent WPF list jumping when clicking checkboxes or controls in non-top rows
        }

        private void Thumbnail_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                ImageFile? img = null;
                if (fe.DataContext is DuplicateGroup group)
                {
                    img = group.PrimaryImage;
                }
                else if (fe.DataContext is DuplicateImage dup)
                {
                    img = dup.Image;
                }

                if (img != null)
                {
                    ShowPreview(img);
                }
            }
        }

        private void Thumbnail_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Space)
            {
                if (sender is FrameworkElement fe)
                {
                    ImageFile? img = null;
                    if (fe.DataContext is DuplicateGroup group)
                    {
                        img = group.PrimaryImage;
                    }
                    else if (fe.DataContext is DuplicateImage dup)
                    {
                        img = dup.Image;
                    }

                    if (img != null)
                    {
                        ShowPreview(img);
                        e.Handled = true;
                    }
                }
            }
        }

        private void ShowPreview(ImageFile img)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
                bitmap.DecodePixelWidth = 1000;
                bitmap.UriSource = new Uri(img.FilePath);
                bitmap.EndInit();
                bitmap.Freeze();

                ImgPreview.Source = bitmap;
                TxtPreviewDimensions.Text = $"{bitmap.PixelWidth} × {bitmap.PixelHeight}";
            }
            catch (Exception)
            {
                ImgPreview.Source = null;
                TxtPreviewDimensions.Text = "Unknown Dimensions";
            }

            TxtPreviewFileName.Text = img.FileName;
            TxtPreviewFilePath.Text = img.FilePath;
            TxtPreviewFileSize.Text = FormatBytes(img.FileSize);
            TxtPreviewDate.Text = img.DateTaken?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown";
            TxtPreviewCamera.Text = !string.IsNullOrEmpty(img.CameraModel) ? img.CameraModel : "Unknown";
            TxtPreviewGps.Text = img.HasGps ? $"{img.Latitude:F5}, {img.Longitude:F5}" : "No GPS Data";

            ColPreview.MinWidth = 240;
            ColPreview.Width = new GridLength(_lastPreviewWidth);
            GrpPreview.Visibility = Visibility.Visible;
        }

        private void ClosePreview_Click(object sender, RoutedEventArgs e)
        {
            if (ColPreview.Width.Value > 0)
            {
                _lastPreviewWidth = ColPreview.Width.Value;
            }
            ColPreview.MinWidth = 0;
            ColPreview.Width = new GridLength(0);
            GrpPreview.Visibility = Visibility.Collapsed;
        }

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = TxtPreviewFilePath.Text;
                if (!string.IsNullOrEmpty(path))
                {
                    Clipboard.SetText(path);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to copy path: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = TxtPreviewFilePath.Text;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to open folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
    }
}