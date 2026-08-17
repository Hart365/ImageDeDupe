using System;
using System.IO;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageDeDupeApp.Helpers
{
    public static class ImageHasher
    {
        public static ulong? CalculateDHash(string filePath)
        {
            var sig = CalculateVisualSignature(filePath);
            return sig?.HashH;
        }

        public static (ulong HashH, ulong HashV, byte[] ColorSig)? CalculateVisualSignature(string filePath)
        {
            try
            {
                BitmapSource source;
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
                    bitmap.DecodePixelWidth = 256; // Natively decode at low resolution in codec (saves ~99% memory & CPU time)
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    source = bitmap;
                }

                // Step 1: Pre-scale to 128x128.
                // This is fast and fits in memory easily, reducing subsequent processing cost.
                double scaleX = 128.0 / source.PixelWidth;
                double scaleY = 128.0 / source.PixelHeight;
                var scaled = new TransformedBitmap(source, new ScaleTransform(scaleX, scaleY));
                
                // Convert to Bgra32
                var bgraSource = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
                
                int srcW = bgraSource.PixelWidth;
                int srcH = bgraSource.PixelHeight;
                int stride = srcW * 4;
                byte[] bgraPixels = new byte[srcH * stride];
                bgraSource.CopyPixels(bgraPixels, stride, 0);

                // Step 2: Downscale to target sizes using area-averaging box filter
                byte[] bgra9x8 = DownscaleBgra(bgraPixels, srcW, srcH, 9, 8);
                byte[] bgra8x9 = DownscaleBgra(bgraPixels, srcW, srcH, 8, 9);
                byte[] bgra4x4 = DownscaleBgra(bgraPixels, srcW, srcH, 4, 4);

                // Step 3: Convert structures to grayscale
                byte[] gray9x8 = ConvertToGrayscale(bgra9x8);
                byte[] gray8x9 = ConvertToGrayscale(bgra8x9);

                // Step 4: Compute horizontal dHash (64-bit) from 9x8 grayscale
                ulong hashH = 0;
                int bitIndex = 0;
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        byte left = gray9x8[y * 9 + x];
                        byte right = gray9x8[y * 9 + x + 1];
                        if (left > right)
                        {
                            hashH |= (1UL << bitIndex);
                        }
                        bitIndex++;
                    }
                }

                // Step 5: Compute vertical dHash (64-bit) from 8x9 grayscale
                ulong hashV = 0;
                bitIndex = 0;
                for (int x = 0; x < 8; x++)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        byte top = gray8x9[y * 8 + x];
                        byte bottom = gray8x9[(y + 1) * 8 + x];
                        if (top > bottom)
                        {
                            hashV |= (1UL << bitIndex);
                        }
                        bitIndex++;
                    }
                }

                // Step 6: Compute color signature (48 bytes for 4x4 RGB grid)
                byte[] colorSig = new byte[48];
                for (int i = 0; i < 16; i++)
                {
                    colorSig[i * 3]     = bgra4x4[i * 4 + 2]; // R
                    colorSig[i * 3 + 1] = bgra4x4[i * 4 + 1]; // G
                    colorSig[i * 3 + 2] = bgra4x4[i * 4];     // B
                }

                return (hashH, hashV, colorSig);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to calculate visual signature for {filePath}: {ex.Message}");
                return null;
            }
        }

        public static double GetVisualSimilarity(ulong h1, ulong v1, byte[] c1, ulong h2, ulong v2, byte[] c2)
        {
            int distH = BitOperations.PopCount(h1 ^ h2);
            int distV = BitOperations.PopCount(v1 ^ v2);
            double structureSim = (128.0 - (distH + distV)) / 128.0 * 100.0;

            double colorSim = 100.0;
            if (c1 != null && c2 != null && c1.Length == 48 && c2.Length == 48)
            {
                double sumDistSq = 0;
                for (int i = 0; i < 16; i++)
                {
                    double dR = (double)c1[i * 3] - c2[i * 3];
                    double dG = (double)c1[i * 3 + 1] - c2[i * 3 + 1];
                    double dB = (double)c1[i * 3 + 2] - c2[i * 3 + 2];
                    sumDistSq += (dR * dR + dG * dG + dB * dB);
                }
                double rmsDist = Math.Sqrt(sumDistSq / 16.0);
                colorSim = Math.Max(0.0, 100.0 - (rmsDist / 128.0) * 100.0);
            }

            // Weighted average: 75% structure, 25% color
            return (structureSim * 0.75) + (colorSim * 0.25);
        }

        private static byte[] DownscaleBgra(byte[] srcPixels, int srcW, int srcH, int dstW, int dstH)
        {
            byte[] dstPixels = new byte[dstW * dstH * 4];
            
            for (int dy = 0; dy < dstH; dy++)
            {
                double yStart = dy * (double)srcH / dstH;
                double yEnd = (dy + 1) * (double)srcH / dstH;
                int yStartInt = (int)Math.Floor(yStart);
                int yEndInt = (int)Math.Min(srcH, Math.Ceiling(yEnd));
                
                for (int dx = 0; dx < dstW; dx++)
                {
                    double xStart = dx * (double)srcW / dstW;
                    double xEnd = (dx + 1) * (double)srcW / dstW;
                    int xStartInt = (int)Math.Floor(xStart);
                    int xEndInt = (int)Math.Min(srcW, Math.Ceiling(xEnd));
                    
                    double sumR = 0, sumG = 0, sumB = 0, sumA = 0;
                    double totalWeight = 0;
                    
                    for (int sy = yStartInt; sy < yEndInt; sy++)
                    {
                        double yWeight = 1.0;
                        if (sy < yStart) yWeight -= (yStart - sy);
                        if (sy + 1 > yEnd) yWeight -= (sy + 1 - yEnd);
                        yWeight = Math.Max(0.0, yWeight);
                        
                        for (int sx = xStartInt; sx < xEndInt; sx++)
                        {
                            double xWeight = 1.0;
                            if (sx < xStart) xWeight -= (xStart - sx);
                            if (sx + 1 > xEnd) xWeight -= (sx + 1 - xEnd);
                            xWeight = Math.Max(0.0, xWeight);
                            
                            double weight = xWeight * yWeight;
                            if (weight > 0)
                            {
                                int srcIndex = (sy * srcW + sx) * 4;
                                sumB += srcPixels[srcIndex] * weight;
                                sumG += srcPixels[srcIndex + 1] * weight;
                                sumR += srcPixels[srcIndex + 2] * weight;
                                sumA += srcPixels[srcIndex + 3] * weight;
                                totalWeight += weight;
                            }
                        }
                    }
                    
                    int dstIndex = (dy * dstW + dx) * 4;
                    if (totalWeight > 0)
                    {
                        dstPixels[dstIndex] = (byte)Math.Clamp(sumB / totalWeight, 0, 255);
                        dstPixels[dstIndex + 1] = (byte)Math.Clamp(sumG / totalWeight, 0, 255);
                        dstPixels[dstIndex + 2] = (byte)Math.Clamp(sumR / totalWeight, 0, 255);
                        dstPixels[dstIndex + 3] = (byte)Math.Clamp(sumA / totalWeight, 0, 255);
                    }
                }
            }
            return dstPixels;
        }

        private static byte[] ConvertToGrayscale(byte[] bgraPixels)
        {
            byte[] gray = new byte[bgraPixels.Length / 4];
            for (int i = 0; i < gray.Length; i++)
            {
                byte b = bgraPixels[i * 4];
                byte g = bgraPixels[i * 4 + 1];
                byte r = bgraPixels[i * 4 + 2];
                gray[i] = (byte)(0.299 * r + 0.587 * g + 0.114 * b);
            }
            return gray;
        }

        public static double GetHammingSimilarity(ulong hash1, ulong hash2)
        {
            ulong xor = hash1 ^ hash2;
            int distance = BitOperations.PopCount(xor);
            return (64 - distance) / 64.0 * 100.0;
        }

        public static double GetLevenshteinSimilarity(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 100.0 : 0.0;
            if (string.IsNullOrEmpty(t)) return 0.0;

            s = s.ToLowerInvariant();
            t = t.ToLowerInvariant();

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            int maxLen = Math.Max(n, m);
            return (maxLen - d[n, m]) / (double)maxLen * 100.0;
        }

        public static double GetGpsDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            double r = 6371000; // Earth's radius in meters
            double phi1 = lat1 * Math.PI / 180.0;
            double phi2 = lat2 * Math.PI / 180.0;
            double deltaPhi = (lat2 - lat1) * Math.PI / 180.0;
            double deltaLambda = (lon2 - lon1) * Math.PI / 180.0;

            double a = Math.Sin(deltaPhi / 2.0) * Math.Sin(deltaPhi / 2.0) +
                       System.Math.Cos(phi1) * System.Math.Cos(phi2) *
                       System.Math.Sin(deltaLambda / 2.0) * System.Math.Sin(deltaLambda / 2.0);
            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

            return r * c;
        }
    }
}
