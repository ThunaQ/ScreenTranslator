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
        private static readonly object sync = new object();
        private static readonly List<TranslationHistoryEntry> entries = new List<TranslationHistoryEntry>();

        /// <summary>Raised after an entry is added or the history is cleared.</summary>
        public static event Action Changed;

        /// <summary>A snapshot, newest first. Safe to enumerate while new entries arrive.</summary>
        public static IReadOnlyList<TranslationHistoryEntry> Entries
        {
            get
            {
                lock (sync)
                    return entries.ToArray();
            }
        }

        public static void Add(string originalText, string translatedText)
        {
            lock (sync)
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

            Changed?.Invoke();
        }

        public static void Clear()
        {
            lock (sync)
                entries.Clear();

            Changed?.Invoke();
        }
    }
}