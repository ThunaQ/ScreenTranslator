using System.Collections.Generic;
using Tesseract;
using Xunit;

namespace ScreenTranslator.Tests
{
    public class TranslationServiceTests
    {
        // --- CleanLine ---

        [Fact]
        public void CleanLine_RemovesDisallowedSymbols()
        {
            string result = TranslationService.CleanLine("Hello¤World★!");
            Assert.Equal("HelloWorld!", result);
        }

        [Fact]
        public void CleanLine_CollapsesMultipleSpaces()
        {
            string result = TranslationService.CleanLine("Hello    World");
            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void CleanLine_TrimsLeadingAndTrailingWhitespace()
        {
            string result = TranslationService.CleanLine("   Hello World   ");
            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void CleanLine_KeepsTurkishAndCyrillicCharacters()
        {
            string result = TranslationService.CleanLine("Işığı öğüt привет");
            Assert.Equal("Işığı öğüt привет", result);
        }

        // --- MergeLines ---

        [Fact]
        public void MergeLines_EmptyList_ReturnsEmptyString()
        {
            string result = TranslationService.MergeLines(new List<(string Text, Rect Bounds)>());
            Assert.Equal("", result);
        }

        [Fact]
        public void MergeLines_WrappedSentence_JoinsWithSpace()
        {
            // İki satır birbirine yakın (küçük dikey boşluk) ve ilk satır noktalama ile bitmiyor.
            var lines = new List<(string Text, Rect Bounds)>
            {
                ("Chelsea Partners with Circle", new Rect(0, 0, 300, 20)),
                ("for USDC Shirt Sponsorship", new Rect(0, 22, 300, 42)) // 2px boşluk, satır yüksekliği 20
            };

            string result = TranslationService.MergeLines(lines);

            Assert.Equal("Chelsea Partners with Circle for USDC Shirt Sponsorship", result);
        }

        [Fact]
        public void MergeLines_SeparateBlock_KeepsLineBreak()
        {
            // Büyük dikey boşluk -> ayrı bir UI elemanı olarak değerlendirilmeli.
            var lines = new List<(string Text, Rect Bounds)>
            {
                ("Chelsea Partners with Circle", new Rect(0, 0, 300, 20)),
                ("2 days ago - Sports - 60K posts", new Rect(0, 60, 300, 80)) // 40px boşluk, satır yüksekliği 20
            };

            string result = TranslationService.MergeLines(lines);

            Assert.Equal("Chelsea Partners with Circle\n2 days ago - Sports - 60K posts", result);
        }

        [Fact]
        public void MergeLines_SentenceEndingPunctuation_StartsNewLine()
        {
            var lines = new List<(string Text, Rect Bounds)>
            {
                ("Welcome back.", new Rect(0, 0, 300, 20)),
                ("Please choose an option.", new Rect(0, 22, 300, 42))
            };

            string result = TranslationService.MergeLines(lines);

            Assert.Equal("Welcome back.\nPlease choose an option.", result);
        }

        // --- ComputeOtsuThreshold ---

        [Fact]
        public void ComputeOtsuThreshold_TwoDistinctClusters_FindsMidpoint()
        {
            // Pikseller ya çok karanlık (10) ya da çok aydınlık (245) - klasik iki tepe.
            var histogram = new int[256];
            histogram[10] = 500;
            histogram[245] = 500;

            int threshold = TranslationService.ComputeOtsuThreshold(histogram, 1000);

            // Eşik iki tepe arasında bir yerde olmalı.
            Assert.InRange(threshold, 10, 245);
        }

        [Fact]
        public void ComputeOtsuThreshold_AllSamePixel_ReturnsWithinRange()
        {
            var histogram = new int[256];
            histogram[128] = 1000;

            int threshold = TranslationService.ComputeOtsuThreshold(histogram, 1000);

            Assert.InRange(threshold, 0, 255);
        }
    }
}