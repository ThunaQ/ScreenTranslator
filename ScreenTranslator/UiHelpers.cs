#nullable disable
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ScreenTranslator
{
    internal static class NativeMethods
    {
        public const uint ModAlt = 0x0001;
        public const uint ModControl = 0x0002;
        public const uint ModShift = 0x0004;
        public const uint ModWin = 0x0008;
        public const uint ModNoRepeat = 0x4000;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>Makes the window's title bar dark (Windows 10 1809+ / Windows 11). Silently does nothing elsewhere.</summary>
        public static void UseDarkTitleBar(Window window)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                int enabled = 1;
                // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE, 19 = the same attribute on pre-2004 Windows 10 builds
                if (DwmSetWindowAttribute(hwnd, 20, ref enabled, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, 19, ref enabled, sizeof(int));
            }
            catch
            {
                // Cosmetic only.
            }
        }

        /// <summary>Checks whether another application already owns this key combination.</summary>
        public static bool IsHotkeyAvailable(IntPtr hwnd, uint modifiers, uint vk)
        {
            const int TestId = 9100;
            if (!RegisterHotKey(hwnd, TestId, modifiers | ModNoRepeat, vk))
                return false;

            UnregisterHotKey(hwnd, TestId);
            return true;
        }
    }

    /// <summary>Tiny file log so silent failures can be diagnosed: %AppData%\ScreenTranslator\log.txt</summary>
    internal static class AppLog
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenTranslator", "log.txt");

        public static string FilePath => LogPath;

        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 512 * 1024)
                    File.Delete(LogPath);

                File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            }
            catch
            {
                // logging must never break the app
            }
        }
    }
}