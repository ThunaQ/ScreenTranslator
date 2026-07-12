using EkranCeviri;
using GTranslate.Translators;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Tesseract;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace ScreenTranslator
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;
        private System.Windows.Forms.NotifyIcon trayIcon;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CTRL = 0x0002;
        private const uint VK_X = 0x58;

        private System.Windows.Point startPoint;
        private bool isDrawing = false;

        public MainWindow()
        {
            InitializeComponent();

            // --- SYSTEM TRAY ICON SETTINGS ---
            trayIcon = new System.Windows.Forms.NotifyIcon();

            // Automatically extracts the embedded application icon
            trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            trayIcon.Text = "ScreenTranslator - Running in background";
            trayIcon.Visible = true;

            // --- RIGHT CLICK CONTEXT MENU ---
            System.Windows.Forms.ContextMenuStrip contextMenu = new System.Windows.Forms.ContextMenuStrip();

            // 1. Option: Settings
            System.Windows.Forms.ToolStripMenuItem settingsItem = new System.Windows.Forms.ToolStripMenuItem("Settings");
            settingsItem.Click += (s, args) =>
            {
                // Safely connect to WPF's main UI thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SettingsWindow settingsWindow = new SettingsWindow();
                    settingsWindow.Topmost = true; // Ensure it opens above all other windows
                    settingsWindow.ShowDialog();
                });
            };

            // 2. Option: Exit
            System.Windows.Forms.ToolStripMenuItem exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
            exitItem.Click += (s, args) =>
            {
                System.Windows.Application.Current.Shutdown();
            };

            // Add buttons to the menu and link to the tray icon
            contextMenu.Items.Add(settingsItem);
            contextMenu.Items.Add(exitItem);
            trayIcon.ContextMenuStrip = contextMenu;
        }

        public static class AppSettings
        {
            public static string SelectedApi = "Google";
            public static string SourceLang = "en";
            public static string TargetLang = "tr";
            public static string TessLang = "eng";

            // Shortcut Key and Windows Startup Settings
            public static int ShortcutSelection = 0; // 0: Ctrl+Alt+X, 1: Ctrl+Shift+C, 2: Alt+Z
            public static bool AutoStart = false;

            private static string settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

            public static void Save()
            {
                // 1. Write settings to JSON file (Persists after app closes)
                var settings = new { SelectedApi, SourceLang, TargetLang, TessLang, ShortcutSelection, AutoStart };
                File.WriteAllText(settingsFile, JsonSerializer.Serialize(settings));

                // 2. Add or remove from Windows Startup (Registry)
                RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (AutoStart)
                    rk.SetValue("ScreenTranslator", Process.GetCurrentProcess().MainModule.FileName);
                else
                    rk.DeleteValue("ScreenTranslator", false);
            }

            public static bool Load()
            {
                if (File.Exists(settingsFile))
                {
                    string json = File.ReadAllText(settingsFile);
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("SelectedApi", out var p)) SelectedApi = p.GetString();
                        if (root.TryGetProperty("SourceLang", out p)) SourceLang = p.GetString();
                        if (root.TryGetProperty("TargetLang", out p)) TargetLang = p.GetString();
                        if (root.TryGetProperty("TessLang", out p)) TessLang = p.GetString();
                        if (root.TryGetProperty("ShortcutSelection", out p)) ShortcutSelection = p.GetInt32();
                        if (root.TryGetProperty("AutoStart", out p)) AutoStart = p.GetBoolean();
                    }
                    return true; // Settings file found, app has been opened before
                }
                return false; // File not found, this is the FIRST launch
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Hide();

            // Try to load memory on startup
            bool alreadyOpened = AppSettings.Load();

            // If it fails to load (first time opening), force the settings menu to appear
            if (!alreadyOpened)
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SettingsWindow settingsWindow = new SettingsWindow();
                    settingsWindow.Topmost = true;
                    settingsWindow.ShowDialog();
                });
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr handle = new WindowInteropHelper(this).Handle;

            // Get shortcut selection from memory
            uint fsModifiers = MOD_CTRL | MOD_ALT;
            uint vk = VK_X; // Default: Ctrl + Alt + X

            if (AppSettings.ShortcutSelection == 1)
            {
                fsModifiers = MOD_CTRL | 0x0004; // 0x0004 = Shift key Windows code
                vk = 0x43; // C key (Ctrl + Shift + C)
            }
            else if (AppSettings.ShortcutSelection == 2)
            {
                fsModifiers = MOD_ALT;
                vk = 0x5A; // Z key (Alt + Z)
            }

            RegisterHotKey(handle, HOTKEY_ID, fsModifiers, vk);

            HwndSource source = HwndSource.FromHwnd(handle);
            source.AddHook(HwndHook);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.WindowState = WindowState.Maximized;
                this.Activate();
                this.Focus();
                SelectionBox.Visibility = Visibility.Collapsed;
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void CanvasArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDrawing = true;
            startPoint = e.GetPosition(CanvasArea);

            Canvas.SetLeft(SelectionBox, startPoint.X);
            Canvas.SetTop(SelectionBox, startPoint.Y);
            SelectionBox.Width = 0;
            SelectionBox.Height = 0;
            SelectionBox.Visibility = Visibility.Visible;

            CanvasArea.CaptureMouse();
        }

        private void CanvasArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDrawing) return;

            System.Windows.Point currentPoint = e.GetPosition(CanvasArea);

            double x = Math.Min(currentPoint.X, startPoint.X);
            double y = Math.Min(currentPoint.Y, startPoint.Y);
            double width = Math.Max(currentPoint.X, startPoint.X) - x;
            double height = Math.Max(currentPoint.Y, startPoint.Y) - y;

            Canvas.SetLeft(SelectionBox, x);
            Canvas.SetTop(SelectionBox, y);
            SelectionBox.Width = width;
            SelectionBox.Height = height;
        }

        // --- NEW: IMAGE PREPROCESSING FILTER ---
        private System.Drawing.Bitmap PreProcessImage(System.Drawing.Bitmap original)
        {
            // 1. Resize (Scale x2 for better pixel clarity on small texts)
            System.Drawing.Bitmap resized = new System.Drawing.Bitmap(original, new System.Drawing.Size(original.Width * 2, original.Height * 2));

            // 2. Grayscale and High Contrast Filter
            System.Drawing.Bitmap result = new System.Drawing.Bitmap(resized.Width, resized.Height);
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(result))
            {
                // Color matrix that kills colors and boosts contrast (Perfect for colorful game backgrounds)
                System.Drawing.Imaging.ColorMatrix colorMatrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
                {
                    new float[] {1.5f, 1.5f, 1.5f, 0, 0},
                    new float[] {1.5f, 1.5f, 1.5f, 0, 0},
                    new float[] {1.5f, 1.5f, 1.5f, 0, 0},
                    new float[] {0, 0, 0, 1, 0},
                    new float[] {-1.0f, -1.0f, -1.0f, 0, 1}
                });

                System.Drawing.Imaging.ImageAttributes attributes = new System.Drawing.Imaging.ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);

                g.DrawImage(resized, new System.Drawing.Rectangle(0, 0, resized.Width, resized.Height),
                    0, 0, resized.Width, resized.Height, System.Drawing.GraphicsUnit.Pixel, attributes);
            }
            return result;
        }

        private async void CanvasArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDrawing = false;
            CanvasArea.ReleaseMouseCapture();

            PresentationSource source = PresentationSource.FromVisual(this);
            double dpiX = 1.0; double dpiY = 1.0;
            if (source != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            this.Hide();

            double wpfX = Canvas.GetLeft(SelectionBox);
            double wpfY = Canvas.GetTop(SelectionBox);
            double wpfWidth = SelectionBox.Width;
            double wpfHeight = SelectionBox.Height;

            if (wpfWidth > 5 && wpfHeight > 5)
            {
                int realX = (int)(wpfX * dpiX);
                int realY = (int)(wpfY * dpiY);
                int realWidth = (int)(wpfWidth * dpiX);
                int realHeight = (int)(wpfHeight * dpiY);

                using (Bitmap bmp = new Bitmap(realWidth, realHeight))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(realX, realY, 0, 0, bmp.Size);
                    }

                    // --- SEND THE RAW IMAGE TO OUR NEW PRE-PROCESSING FILTER ---
                    using (Bitmap processedBmp = PreProcessImage(bmp))
                    {
                        try
                        {
                            string tessDataPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                            byte[] imageBytes;
                            using (var stream = new System.IO.MemoryStream())
                            {
                                // Pass the filtered image to Tesseract
                                processedBmp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                                imageBytes = stream.ToArray();
                            }

                            using (var engine = new TesseractEngine(tessDataPath, AppSettings.TessLang, EngineMode.LstmOnly))
                            {
                                using (var img = Pix.LoadFromMemory(imageBytes))
                                {
                                    // Auto mode (PSM 3) resolves complex layouts better
                                    using (var page = engine.Process(img, PageSegMode.Auto))
                                    {
                                        string extractedText = page.GetText();

                                        if (!string.IsNullOrWhiteSpace(extractedText))
                                        {
                                            // 1. ALLOW RUSSIAN CHARACTERS (Cyrillic Alphabet)
                                            extractedText = Regex.Replace(extractedText, @"[^a-zA-Z0-9\s.,?!'üğişçöÜĞİŞÇÖа-яА-ЯёЁ-]", "");

                                            // 2. Merge hyphenation at the end of lines (e.g., "trans-\nlation" -> "translation")
                                            extractedText = extractedText.Replace("-\n", "").Replace("-\r\n", "");

                                            // 3. SMART LINE MERGING (Prevents sentence fragmentation)
                                            // Converts single Enters into spaces, but preserves double Enters.
                                            extractedText = Regex.Replace(extractedText, @"(?<!\r?\n)\r?\n(?!\r?\n)", " ");

                                            // 4. Clean up excessive consecutive spaces
                                            extractedText = Regex.Replace(extractedText, @"[ \t]+", " ").Trim();

                                            if (extractedText.Length > 3)
                                            {
                                                string translatedText = "";

                                                // Use the selected API from settings
                                                if (AppSettings.SelectedApi == "Google")
                                                {
                                                    var googleTranslator = new GoogleTranslator();
                                                    var result = await googleTranslator.TranslateAsync(extractedText, AppSettings.TargetLang, AppSettings.SourceLang);
                                                    translatedText = result.Translation;
                                                }
                                                else
                                                {
                                                    var yandexTranslator = new YandexTranslator();
                                                    var result = await yandexTranslator.TranslateAsync(extractedText, AppSettings.TargetLang, AppSettings.SourceLang);
                                                    translatedText = result.Translation;
                                                }

                                                ShowElegantTranslationBox(translatedText, wpfX, wpfY + wpfHeight + 10, wpfWidth);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Swallow silently to prevent crashing
                        }
                    }
                }
            }
        }

        // --- NEW: DRAGGABLE ELEGANT BOX ---
        private void ShowElegantTranslationBox(string translation, double leftX, double topY, double boxWidth)
        {
            Window translationWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                SizeToContent = SizeToContent.Height,
                Width = Math.Max(250, boxWidth),
                Left = leftX,
                Top = topY
            };

            Border frame = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(240, 25, 25, 25)),
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 60, 60, 60)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand // Shows a hand icon when hovering over the box
            };

            // NEW: The thick top bar is gone. You can drag the box from anywhere!
            frame.MouseLeftButtonDown += (s, e) => { translationWindow.DragMove(); };

            Grid mainGrid = new Grid();

            // Close Button (X) - Elegantly placed top right
            TextBlock btnClose = new TextBlock
            {
                Text = "✕",
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 5, 8, 0),
                Cursor = Cursors.Arrow // Normal cursor when hovering over X
            };
            btnClose.MouseEnter += (s, e) => btnClose.Foreground = System.Windows.Media.Brushes.Red;
            btnClose.MouseLeave += (s, e) => btnClose.Foreground = System.Windows.Media.Brushes.Gray;
            btnClose.MouseDown += (s, e) => translationWindow.Close();

            // Translated Text
            TextBlock txtTranslation = new TextBlock
            {
                Text = translation.Trim(),
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Medium,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(15, 20, 15, 15) // Slight top margin to avoid overlap with X
            };

            mainGrid.Children.Add(txtTranslation);
            mainGrid.Children.Add(btnClose); // Add X button to the top layer

            frame.Child = mainGrid;
            translationWindow.Content = frame;

            // Out of bounds screen control
            translationWindow.Loaded += (s, e) =>
            {
                if (translationWindow.Left + translationWindow.ActualWidth > SystemParameters.PrimaryScreenWidth)
                    translationWindow.Left = SystemParameters.PrimaryScreenWidth - translationWindow.ActualWidth - 10;
                if (translationWindow.Top + translationWindow.ActualHeight > SystemParameters.PrimaryScreenHeight)
                    translationWindow.Top = SystemParameters.PrimaryScreenHeight - translationWindow.ActualHeight - 10;
            };

            translationWindow.Show();
        }

        protected override void OnClosed(EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(handle, HOTKEY_ID);

            // Destroy the tray icon when the application closes
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }

            base.OnClosed(e);
        }
    }
}