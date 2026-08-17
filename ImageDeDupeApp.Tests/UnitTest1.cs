using System;
using Xunit;
using Xunit.Abstractions;
using ImageDeDupeApp.Helpers;

namespace ImageDeDupeApp.Tests
{
    public class ImageHasherTests
    {
        private readonly ITestOutputHelper _output;

        public ImageHasherTests(ITestOutputHelper output)
        {
            _output = output;
        }
        [Fact]
        public void GetHammingSimilarity_IdenticalHashes_Returns100Percent()
        {
            ulong hash = 0xABCDEF1234567890;
            double similarity = ImageHasher.GetHammingSimilarity(hash, hash);
            Assert.Equal(100.0, similarity);
        }

        [Fact]
        public void GetHammingSimilarity_OppositeHashes_Returns0Percent()
        {
            ulong hash1 = 0x0000000000000000;
            ulong hash2 = 0xFFFFFFFFFFFFFFFF;
            double similarity = ImageHasher.GetHammingSimilarity(hash1, hash2);
            Assert.Equal(0.0, similarity);
        }

        [Fact]
        public void GetHammingSimilarity_HalfDifferingBits_Returns50Percent()
        {
            ulong hash1 = 0xF0F0F0F0F0F0F0F0;
            ulong hash2 = 0x0F0F0F0F0F0F0F0F;
            double similarity = ImageHasher.GetHammingSimilarity(hash1, hash2);
            Assert.Equal(0.0, similarity); // they are completely opposite, so 0%

            ulong hash3 = 0x00000000FFFFFFFF;
            ulong hash4 = 0x0000000000000000;
            double similarity2 = ImageHasher.GetHammingSimilarity(hash3, hash4);
            Assert.Equal(50.0, similarity2); // 32 bits differ, so 50%
        }

        [Fact]
        public void GetLevenshteinSimilarity_IdenticalStrings_Returns100Percent()
        {
            string s1 = "photo_dsc_1024";
            string s2 = "photo_dsc_1024";
            double similarity = ImageHasher.GetLevenshteinSimilarity(s1, s2);
            Assert.Equal(100.0, similarity);
        }

        [Fact]
        public void GetLevenshteinSimilarity_CompletelyDifferentStrings_ReturnsLowOrZeroPercent()
        {
            string s1 = "abc";
            string s2 = "xyz";
            double similarity = ImageHasher.GetLevenshteinSimilarity(s1, s2);
            Assert.Equal(0.0, similarity);
        }

        [Fact]
        public void GetLevenshteinSimilarity_SlightlyDifferentStrings_ReturnsCorrectPercent()
        {
            string s1 = "photo_1";
            string s2 = "photo_2"; // 1 char difference in length 7
            double similarity = ImageHasher.GetLevenshteinSimilarity(s1, s2);
            double expected = (7.0 - 1.0) / 7.0 * 100.0;
            Assert.Equal(expected, similarity, 5);
        }

        [Fact]
        public void GetGpsDistanceMeters_LondonToParis_ReturnsApproximateDistance()
        {
            // London: 51.5074 N, 0.1278 W
            double lat1 = 51.5074;
            double lon1 = -0.1278;

            // Paris: 48.8566 N, 2.3522 E
            double lat2 = 48.8566;
            double lon2 = 2.3522;

            double distance = ImageHasher.GetGpsDistanceMeters(lat1, lon1, lat2, lon2);

            // Distance is approx 344 km (344,000 meters)
            Assert.True(distance > 340000 && distance < 350000, $"Distance was {distance} meters");
        }

        [Fact]
        public void GetGpsDistanceMeters_SameCoordinates_ReturnsZero()
        {
            double lat = 37.7749;
            double lon = -122.4194;

            double distance = ImageHasher.GetGpsDistanceMeters(lat, lon, lat, lon);
            Assert.Equal(0.0, distance, 3);
        }

        [Fact]
        public void GetVisualSimilarity_IdenticalSignatures_Returns100Percent()
        {
            ulong hashH = 0xABCDEF1234567890;
            ulong hashV = 0x0987654321FEDCBA;
            byte[] colorSig = new byte[48];
            for (int i = 0; i < 48; i++) colorSig[i] = (byte)(i * 5);

            double similarity = ImageHasher.GetVisualSimilarity(hashH, hashV, colorSig, hashH, hashV, colorSig);
            Assert.Equal(100.0, similarity);
        }

        [Fact]
        public void GetVisualSimilarity_DifferentSignatures_ReturnsLowerSimilarity()
        {
            ulong hashH1 = 0x0000000000000000;
            ulong hashV1 = 0x0000000000000000;
            byte[] colorSig1 = new byte[48]; // All black (0)

            ulong hashH2 = 0xFFFFFFFFFFFFFFFF;
            ulong hashV2 = 0xFFFFFFFFFFFFFFFF;
            byte[] colorSig2 = new byte[48];
            for (int i = 0; i < 48; i++) colorSig2[i] = 255; // All white (255)

            double similarity = ImageHasher.GetVisualSimilarity(hashH1, hashV1, colorSig1, hashH2, hashV2, colorSig2);
            // Structure similarity is 0%, Color similarity is 0%
            Assert.Equal(0.0, similarity);
        }
    }
}
