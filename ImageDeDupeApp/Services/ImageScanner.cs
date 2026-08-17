using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageDeDupeApp.Models;
using ImageDeDupeApp.Helpers;

namespace ImageDeDupeApp.Services
{
    public class ImageScanner
    {
        public static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif" };

        public async Task<List<DuplicateGroup>> ScanDirectoryAsync(
            string folderPath,
            bool includeSubfolders,
            ScanOptions options,
            IProgress<(int current, int total, string currentFile)> progress,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                // Find all image files
                var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var files = Directory.EnumerateFiles(folderPath, "*.*", searchOption)
                    .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .Where(file => !IsInDuplicatesSubfolder(file, folderPath))
                    .ToList();

                int totalFiles = files.Count;
                var imageFiles = new List<ImageFile>();

                // Phase 1: Load metadata and hashes
                for (int i = 0; i < totalFiles; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var filePath = files[i];
                    progress.Report((i + 1, totalFiles * 2, $"Reading {Path.GetFileName(filePath)} ({i + 1}/{totalFiles})..."));

                    var img = new ImageFile(filePath);
                    img.LoadMetadata();
                    
                    if (options.CompareVisual)
                    {
                        var sig = ImageHasher.CalculateVisualSignature(filePath);
                        if (sig.HasValue)
                        {
                            img.DifferenceHash = sig.Value.HashH;
                            img.VerticalHash = sig.Value.HashV;
                            img.ColorSignature = sig.Value.ColorSig;
                        }
                    }
                    if (options.CompareFileContents)
                    {
                        img.LoadFileHash();
                    }

                    imageFiles.Add(img);
                }

                // Sort files: chronologically (earliest first), then alphabetically by path
                var sortedImages = imageFiles
                    .OrderBy(img => img.DateTaken ?? DateTime.MaxValue)
                    .ThenBy(img => img.FilePath)
                    .ToList();

                var groups = new List<DuplicateGroup>();
                var classified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Phase 2: Identify duplicates
                for (int i = 0; i < sortedImages.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var img = sortedImages[i];
                    progress.Report((totalFiles + i + 1, totalFiles * 2, $"Comparing {img.FileName} ({i + 1}/{totalFiles})..."));

                    if (classified.Contains(img.FilePath))
                    {
                        continue;
                    }

                    DuplicateGroup? currentGroup = null;

                    for (int j = i + 1; j < sortedImages.Count; j++)
                    {
                        var otherImg = sortedImages[j];
                        if (classified.Contains(otherImg.FilePath))
                        {
                            continue;
                        }

                        double similarity = ComputeSimilarity(img, otherImg, options);
                        if (similarity >= options.SimilarityThreshold)
                        {
                            if (currentGroup == null)
                            {
                                currentGroup = new DuplicateGroup(img);
                            }
                            
                            currentGroup.Duplicates.Add(new DuplicateImage(otherImg, similarity));
                            classified.Add(otherImg.FilePath);
                        }
                    }

                    if (currentGroup != null)
                    {
                        groups.Add(currentGroup);
                        classified.Add(img.FilePath);
                    }
                }

                progress.Report((totalFiles * 2, totalFiles * 2, "Scan complete!"));
                return groups;
            }, cancellationToken);
        }

        private double ComputeSimilarity(ImageFile img1, ImageFile img2, ScanOptions options)
        {
            var scores = new List<double>();

            // 1. File contents check (binary comparison)
            if (options.CompareFileContents)
            {
                img1.LoadFileHash();
                img2.LoadFileHash();
                bool matches = !string.IsNullOrEmpty(img1.FileHash) && img1.FileHash == img2.FileHash;
                double score = matches ? 100.0 : 0.0;

                // Special optimization: In OR mode, if file contents match exactly, return 100% immediately
                if (!options.MatchAllCriteria && matches)
                {
                    return 100.0;
                }

                scores.Add(score);
            }

            // 2. Visual similarity (dHash + color)
            if (options.CompareVisual)
            {
                if (img1.DifferenceHash.HasValue && img1.VerticalHash.HasValue && img1.ColorSignature != null &&
                    img2.DifferenceHash.HasValue && img2.VerticalHash.HasValue && img2.ColorSignature != null)
                {
                    double sim = ImageHasher.GetVisualSimilarity(
                        img1.DifferenceHash.Value, img1.VerticalHash.Value, img1.ColorSignature,
                        img2.DifferenceHash.Value, img2.VerticalHash.Value, img2.ColorSignature);
                    scores.Add(sim);
                }
                else
                {
                    // Visual hash is enabled but could not be computed
                    scores.Add(0.0);
                }
            }

            // 3. Date & Time
            if (options.CompareDateTime)
            {
                if (img1.DateTaken.HasValue && img2.DateTaken.HasValue)
                {
                    double diffSec = Math.Abs((img1.DateTaken.Value - img2.DateTaken.Value).TotalSeconds);
                    double timeSim = 0.0;
                    if (diffSec <= 2) timeSim = 100.0;
                    else if (diffSec <= 10) timeSim = 98.0;
                    else if (diffSec <= 60) timeSim = 95.0;
                    else if (diffSec <= 3600) timeSim = 95.0 - (diffSec / 3600.0) * 45.0;
                    else if (diffSec <= 86400) timeSim = 50.0 - (diffSec / 86400.0) * 50.0;
                    
                    scores.Add(timeSim);
                }
                else
                {
                    scores.Add(0.0); // No date metadata
                }
            }

            // 4. GPS Location
            if (options.CompareLocation)
            {
                if (img1.HasGps && img2.HasGps)
                {
                    double dist = ImageHasher.GetGpsDistanceMeters(img1.Latitude!.Value, img1.Longitude!.Value, img2.Latitude!.Value, img2.Longitude!.Value);
                    double locSim = 0.0;
                    if (dist <= 5) locSim = 100.0;
                    else if (dist <= 20) locSim = 100.0 - (dist - 5) * (10.0 / 15.0);
                    else if (dist <= 50) locSim = 90.0 - (dist - 20) * (20.0 / 30.0);
                    else if (dist <= 100) locSim = 70.0 - (dist - 50) * (50.0 / 50.0);
                    else if (dist <= 200) locSim = 20.0 - (dist - 100) * (20.0 / 100.0);
                    
                    scores.Add(locSim);
                }
                else
                {
                    scores.Add(0.0); // No GPS metadata
                }
            }

            // 5. Filename
            if (options.CompareFilename)
            {
                string n1 = Path.GetFileNameWithoutExtension(img1.FileName);
                string n2 = Path.GetFileNameWithoutExtension(img2.FileName);
                double nameSim = ImageHasher.GetLevenshteinSimilarity(n1, n2);
                scores.Add(nameSim);
            }

            // 6. File Size
            if (options.CompareFileSize)
            {
                long s1 = img1.FileSize;
                long s2 = img2.FileSize;
                double sizeSim = 0.0;
                if (s1 == 0 && s2 == 0) sizeSim = 100.0;
                else sizeSim = (double)Math.Min(s1, s2) / Math.Max(s1, s2) * 100.0;
                
                scores.Add(sizeSim);
            }

            if (scores.Count == 0) return 0.0;

            if (options.MatchAllCriteria)
            {
                // AND mode: All enabled criteria must meet the threshold
                bool allMatch = scores.All(s => s >= options.SimilarityThreshold);
                return allMatch ? scores.Average() : 0.0;
            }
            else
            {
                // OR mode: At least one enabled criteria must meet the threshold
                bool anyMatch = scores.Any(s => s >= options.SimilarityThreshold);
                return anyMatch ? scores.Max() : 0.0;
            }
        }

        private static bool IsInDuplicatesSubfolder(string filePath, string rootPath)
        {
            try
            {
                string relativePath = Path.GetRelativePath(rootPath, filePath);
                string[] segments = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                
                // If any segment except the last one (which is the filename) is "Duplicates", ignore it
                for (int i = 0; i < segments.Length - 1; i++)
                {
                    if (string.Equals(segments[i], "Duplicates", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Fallback to simple substring match if relative path calculation fails
                string duplicatesDirPattern1 = $"{Path.DirectorySeparatorChar}Duplicates{Path.DirectorySeparatorChar}";
                string duplicatesDirPattern2 = $"{Path.AltDirectorySeparatorChar}Duplicates{Path.AltDirectorySeparatorChar}";
                
                if (filePath.Contains(duplicatesDirPattern1, StringComparison.OrdinalIgnoreCase) ||
                    filePath.Contains(duplicatesDirPattern2, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public class ScanOptions
    {
        public bool CompareVisual { get; set; } = true;
        public bool CompareFileContents { get; set; } = false;
        public bool CompareDateTime { get; set; } = false;
        public bool CompareLocation { get; set; } = false;
        public bool CompareFilename { get; set; } = false;
        public bool CompareFileSize { get; set; } = false;
        public double SimilarityThreshold { get; set; } = 90.0;
        public bool MatchAllCriteria { get; set; } = false; // false = OR, true = AND
    }
}
