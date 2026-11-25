#nullable disable
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;

namespace ScreenTranslator
{
    public sealed class AppLanguage
    {
        public AppLanguage(string code, string tess)
        {
            Code = code;
            Tess = tess;
        }

        /// <summary>Code sent to the translation API (en, tr, ...).</summary>
        public string Code { get; }

        /// <summary>Tesseract traineddata name (eng, tur, ...).</summary>
        public string Tess { get; }

        /// <summary>Display name in the current interface language.</summary>
        public string Name => Loc.T("lang." + Code);
    }

    public static class AppLanguages
    {
        public static readonly IReadOnlyList<AppLanguage> All = new[]
        {
            new AppLanguage("en", "eng"),
            new AppLanguage("tr", "tur"),
            new AppLanguage("ru", "rus"),
            new AppLanguage("es", "spa"),
            new AppLanguage("fr", "fra"),
            new AppLanguage("it", "ita"),
            new AppLanguage("de", "deu"),
        };

        public static AppLanguage Find(string code) =>
            All.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    public static class AppSettings
    {
        public static string SelectedApi = "Google";
        public static string SourceLang = "en";
        public static string TargetLang = "tr";
        public static bool AutoDetectSource = false;
        public static bool AutoStart = false;

        public const uint DefaultHotkeyModifiers = NativeMethods.ModControl | NativeMethods.ModAlt;
        public const uint DefaultHotkeyKey = 0x58; // X

        public static uint HotkeyModifiers = DefaultHotkeyModifiers;
        public static uint HotkeyKey = DefaultHotkeyKey;

        /// <summary>Interface language (en, tr). Backed by Loc so the whole app switches together.</summary>
        public static string UiLanguage
        {
            get => Loc.Current;
            set => Loc.Current = value;
        }

        /// <summary>Tesseract language of the selected source language, derived so it can never get out of sync.</summary>
        public static string TessLang => AppLanguages.Find(SourceLang)?.Tess ?? "eng";

        public static string HotkeyDisplay => FormatHotkey(HotkeyModifiers, HotkeyKey);

        private const string RunKeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string AppRegistryName = "ScreenTranslator";

        private static readonly string SettingsDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenTranslator");

        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

        // Older versions wrote settings.json next to the executable.
        private static readonly string LegacySettingsFilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        private sealed class SettingsData
        {
            public string SelectedApi { get; set; }
            public string SourceLang { get; set; }
            public string TargetLang { get; set; }
            public bool? AutoDetectSource { get; set; }
            public bool? AutoStart { get; set; }
            public uint? HotkeyModifiers { get; set; }
            public uint? HotkeyKey { get; set; }
            public int? ShortcutSelection { get; set; } // legacy: 0, 1, 2
            public string UiLanguage { get; set; }
        }

        /// <summary>Loads saved settings. Returns false when nothing usable was found (first run).</summary>
        public static bool Load()
        {
            string path = File.Exists(SettingsFilePath) ? SettingsFilePath
                        : File.Exists(LegacySettingsFilePath) ? LegacySettingsFilePath
                        : null;

            if (path == null)
                return false;

            try
            {
                var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(path));
                if (data == null)
                    return false;

                Apply(data);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScreenTranslator] Could not read settings: {ex.Message}");
                return false;
            }

            if (path == LegacySettingsFilePath)
            {
                try { Save(); } catch { /* migration is best effort */ }
            }

            return true;
        }

        private static void Apply(SettingsData data)
        {
            if (data.SelectedApi == "Google" || data.SelectedApi == "Yandex")
                SelectedApi = data.SelectedApi;

            if (AppLanguages.Find(data.SourceLang) != null) SourceLang = data.SourceLang;
            if (AppLanguages.Find(data.TargetLang) != null) TargetLang = data.TargetLang;

            AutoDetectSource = data.AutoDetectSource ?? false;
            AutoStart = data.AutoStart ?? false;
            if (Loc.IsSupported(data.UiLanguage)) Loc.Current = data.UiLanguage;

            if (data.HotkeyModifiers.HasValue && data.HotkeyKey.HasValue &&
                data.HotkeyModifiers.Value != 0 && data.HotkeyKey.Value != 0)
            {
                HotkeyModifiers = data.HotkeyModifiers.Value;
                HotkeyKey = data.HotkeyKey.Value;
            }
            else if (data.ShortcutSelection.HasValue)
            {
                switch (data.ShortcutSelection.Value)
                {
                    case 1:
                        HotkeyModifiers = NativeMethods.ModControl | NativeMethods.ModShift;
                        HotkeyKey = 0x43; // C
                        break;
                    case 2:
                        HotkeyModifiers = NativeMethods.ModAlt;
                        HotkeyKey = 0x5A; // Z
                        break;
                    default:
                        HotkeyModifiers = DefaultHotkeyModifiers;
                        HotkeyKey = DefaultHotkeyKey;
                        break;
                }
            }
        }

        public static void Save()
        {
            Directory.CreateDirectory(SettingsDirectory);

            var data = new SettingsData
            {
                SelectedApi = SelectedApi,
                SourceLang = SourceLang,
                TargetLang = TargetLang,
                AutoDetectSource = AutoDetectSource,
                AutoStart = AutoStart,
                HotkeyModifiers = HotkeyModifiers,
                HotkeyKey = HotkeyKey,
                UiLanguage = Loc.Current,
            };

            File.WriteAllText(SettingsFilePath,
                JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));

            ApplyAutoStart();
        }

        private static void ApplyAutoStart()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null)
                        return;

                    if (AutoStart)
                    {
                        string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                            key.SetValue(AppRegistryName, "\"" + exePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue(AppRegistryName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScreenTranslator] Could not update the startup entry: {ex.Message}");
            }
        }

        public static string FormatHotkey(uint modifiers, uint vk)
        {
            var parts = new List<string>();
            if ((modifiers & NativeMethods.ModControl) != 0) parts.Add("Ctrl");
            if ((modifiers & NativeMethods.ModAlt) != 0) parts.Add("Alt");
            if ((modifiers & NativeMethods.ModShift) != 0) parts.Add("Shift");
            if ((modifiers & NativeMethods.ModWin) != 0) parts.Add("Win");

            Key key = KeyInterop.KeyFromVirtualKey((int)vk);
            parts.Add(KeyName(key));
            return string.Join(" + ", parts);
        }

        private static string KeyName(Key key)
        {
            if (key >= Key.D0 && key <= Key.D9) return ((int)key - (int)Key.D0).ToString();
            if (key >= Key.NumPad0 && key <= Key.NumPad9) return "Num " + ((int)key - (int)Key.NumPad0);

            switch (key)
            {
                case Key.Return: return "Enter";
                case Key.Prior: return "Page Up";
                case Key.Next: return "Page Down";
                case Key.Back: return "Backspace";
                case Key.Capital: return "Caps Lock";
                case Key.Oem3: return "`";
                case Key.OemPlus: return "+";
                case Key.OemMinus: return "-";
                case Key.OemComma: return ",";
                case Key.OemPeriod: return ".";
                default: return key.ToString();
            }
        }
    }
}