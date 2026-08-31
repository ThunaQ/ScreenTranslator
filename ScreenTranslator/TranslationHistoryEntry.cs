using System;
using System.Collections.Generic;

namespace ScreenTranslator
{
    public class TranslationHistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public string OriginalText { get; set; }
        public string TranslatedText { get; set; }
    }

    public static class TranslationHistory
    {
        private const int MaxEntries = 20;
        private static readonly List<TranslationHistoryEntry> entries = new List<TranslationHistoryEntry>();

        public static IReadOnlyList<TranslationHistoryEntry> Entries => entries;

        public static void Add(string originalText, string translatedText)
        {
            entries.Insert(0, new TranslationHistoryEntry
            {
                Timestamp = DateTime.Now,
                OriginalText = originalText,
                TranslatedText = translatedText
            });

            while (entries.Count > MaxEntries)
                entries.RemoveAt(entries.Count - 1);
        }

        public static void Clear() => entries.Clear();
    }
}