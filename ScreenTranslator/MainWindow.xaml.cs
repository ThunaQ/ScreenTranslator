using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CTRL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_X = 0x58;
        private const uint VK_C = 0x43;
        private const uint VK_Z = 0x5A;

        private System.Windows.Forms.NotifyIcon trayIcon;
        private System.Windows.Point startPoint;
        private bool isDrawing = false;

        private readonly TranslationService translationService = new TranslationService();
        private Window currentTranslationWindow;

        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location),
                Text = "ScreenTranslator - Running in background",
                Visible = true
            };

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();

            var settingsItem = new System.Windows.Forms.ToolStripMenuItem("Settings");
            settingsItem.Click += (s, args) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var settingsWindow = new SettingsWindow { Topmost = true };
                    settingsWindow.ShowDialog();
                });
            };

            var historyItem = new System.Windows.Forms.ToolStripMenuItem("History");
            historyItem.Click += (s, args) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var historyWindow = new HistoryWindow { Topmost = true };
                    historyWindow.Show();
                });
            };

            var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
            exitItem.Click += (s, args) => System.Windows.Application.Current.Shutdown();

            contextMenu.Items.Add(settingsItem);
            contextMenu.Items.Add(historyItem);
            contextMenu.Items.Add(exitItem);
            trayIcon.ContextMenuStrip = contextMenu;
        }

        public static class AppSettings
        {
            public static string SelectedApi = "Google";
            public static string SourceLang = "en";
            public static string TargetLang = "tr";
            public static string TessLang = "eng";
            public static int ShortcutSelection = 0;
            public static bool AutoStart = false;
            public static bool AutoDetectSource = false;

            private const string RunKeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
            private const string AppRegistryName = "ScreenTranslator";
            private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

            public static void Save()
            {
                var settings = new { SelectedApi, SourceLang, TargetLang, TessLang, ShortcutSelection, AutoStart, AutoDetectSource };
                File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings));

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key != null)
                    {
                        if (AutoStart)
                            key.SetValue(AppRegistryName, Process.GetCurrentProcess().MainModule.FileName);
                        else
                            key.DeleteValue(AppRegistryName, false);
                    }
                }
            }

            public static bool Load()
            {
                if (!File.Exists(SettingsFilePath))
                    return false;

                string json = File.ReadAllText(SettingsFilePath);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("SelectedApi", out var p)) SelectedApi = p.GetString();
                    if (root.TryGetProperty("SourceLang", out p)) SourceLang = p.GetString();
                    if (root.TryGetProperty("TargetLang", out p)) TargetLang = p.GetString();
                    if (root.TryGetProperty("TessLang", out p)) TessLang = p.GetString();
                    if (root.TryGetProperty("ShortcutSelection", out p)) ShortcutSelection = p.GetInt32();
                    if (root.TryGetProperty("AutoStart", out p)) AutoStart = p.GetBoolean();
                    if (root.TryGetProperty("AutoDetectSource", out p)) AutoDetectSource = p.GetBoolean();
                }
                return true;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Hide();
            bool alreadyOpened = AppSettings.Load();

            if (!alreadyOpened)
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var settingsWindow = new SettingsWindow { Topmost = true };
                    settingsWindow.ShowDialog();
                });
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr handle = new WindowInteropHelper(this).Handle;

            uint fsModifiers = MOD_CTRL | MOD_ALT;
            uint vk = VK_X;

            if (AppSettings.ShortcutSelection == 1)
            {
                fsModifiers = MOD_CTRL | MOD_SHIFT;
                vk = VK_C;
            }
            else if (AppSettings.ShortcutSelection == 2)
            {
                fsModifiers = MOD_ALT;
                vk = VK_Z;
            }

            bool registered = RegisterHotKey(handle, HOTKEY_ID, fsModifiers, vk);
            if (!registered)
            {
                System.Windows.MessageBox.Show(
                    "Could not register the shortcut key. It may already be in use by another application. Please pick a different shortcut in Settings.",
                    "ScreenTranslator", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

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

        private async void CanvasArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDrawing = false;
            CanvasArea.ReleaseMouseCapture();

            System.Windows.Point releasePoint = e.GetPosition(CanvasArea);

            double wpfX = Canvas.GetLeft(SelectionBox);
            double wpfY = Canvas.GetTop(SelectionBox);
            double wpfWidth = SelectionBox.Width;
            double wpfHeight = SelectionBox.Height;

            if (wpfWidth <= 5 || wpfHeight <= 5)
            {
                this.Hide();
                return;
            }

            System.Windows.Point topLeft = CanvasArea.PointToScreen(new System.Windows.Point(wpfX, wpfY));
            System.Windows.Point bottomRight = CanvasArea.PointToScreen(new System.Windows.Point(wpfX + wpfWidth, wpfY + wpfHeight));

            this.Hide();

            int realX = (int)topLeft.X;
            int realY = (int)topLeft.Y;
            int realWidth = (int)(bottomRight.X - topLeft.X);
            int realHeight = (int)(bottomRight.Y - topLeft.Y);

            using (Bitmap bmp = new Bitmap(realWidth, realHeight))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(realX, realY, 0, 0, bmp.Size);
                }

                try
                {
                    TranslationResult result = await translationService.TranslateCaptureAsync(
                        bmp, AppSettings.SourceLang, AppSettings.TargetLang, AppSettings.TessLang,
                        AppSettings.SelectedApi, AppSettings.AutoDetectSource);

                    Debug.WriteLine($"[ScreenTranslator] OCR output -> \"{result.ExtractedText.Replace("\n", " | ")}\"");

                    if (!result.HasContent)
                    {
                        await FlashFailureAsync(releasePoint.X, releasePoint.Y);
                        return;
                    }

                    TranslationHistory.Add(result.ExtractedText, result.TranslatedText);

                    ShowElegantTranslationBox(result.TranslatedText, wpfX, wpfY + wpfHeight + 10, wpfWidth);
                }
                catch (System.Net.Http.HttpRequestException httpEx)
                {
                    Debug.WriteLine($"[ScreenTranslator] Translation API request failed: {httpEx.Message}. " +
                        "If this says 429 (Too Many Requests), the free translation endpoint is rate-limiting you — wait a bit or switch API in Settings.");
                    await FlashFailureAsync(releasePoint.X, releasePoint.Y);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ScreenTranslator] Capture/translate failed: {ex}");
                    await FlashFailureAsync(releasePoint.X, releasePoint.Y);
                }
            }
        }

        private async Task FlashFailureAsync(double centerX, double centerY)
        {
            SelectionBox.Visibility = Visibility.Collapsed;

            double markSize = ErrorMark.Width;
            Canvas.SetLeft(ErrorMark, centerX - markSize / 2);
            Canvas.SetTop(ErrorMark, centerY - markSize / 2);
            ErrorMark.Visibility = Visibility.Visible;

            System.Windows.Media.Brush originalBackground = this.Background;
            this.Background = System.Windows.Media.Brushes.Transparent;
            this.Show();

            await Task.Delay(350);

            this.Hide();
            ErrorMark.Visibility = Visibility.Collapsed;
            this.Background = originalBackground;
        }

        private void ShowElegantTranslationBox(string translation, double leftX, double topY, double boxWidth)
        {
            currentTranslationWindow?.Close();

            var translationWindow = new Window
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

            var frame = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(240, 25, 25, 25)),
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 60, 60, 60)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };

            frame.MouseLeftButtonDown += (s, e) => translationWindow.DragMove();

            var mainGrid = new Grid();

            var btnCopy = new TextBlock
            {
                Text = "⧉",
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 5, 30, 0),
                Cursor = Cursors.Hand,
                ToolTip = "Copy translation"
            };
            btnCopy.MouseEnter += (s, e) => btnCopy.Foreground = System.Windows.Media.Brushes.White;
            btnCopy.MouseLeave += (s, e) => btnCopy.Foreground = System.Windows.Media.Brushes.Gray;
            btnCopy.MouseDown += (s, e) =>
            {
                e.Handled = true;
                try { System.Windows.Clipboard.SetText(translation); } catch { }
            };

            var btnClose = new TextBlock
            {
                Text = "✕",
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 5, 8, 0),
                Cursor = Cursors.Arrow
            };
            btnClose.MouseEnter += (s, e) => btnClose.Foreground = System.Windows.Media.Brushes.Red;
            btnClose.MouseLeave += (s, e) => btnClose.Foreground = System.Windows.Media.Brushes.Gray;
            btnClose.MouseDown += (s, e) => translationWindow.Close();

            var txtTranslation = new TextBlock
            {
                Text = translation.Trim(),
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Medium,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(15, 20, 15, 15)
            };

            mainGrid.Children.Add(txtTranslation);
            mainGrid.Children.Add(btnCopy);
            mainGrid.Children.Add(btnClose);

            frame.Child = mainGrid;
            translationWindow.Content = frame;

            translationWindow.Loaded += (s, e) =>
            {
                if (translationWindow.Left + translationWindow.ActualWidth > SystemParameters.PrimaryScreenWidth)
                    translationWindow.Left = SystemParameters.PrimaryScreenWidth - translationWindow.ActualWidth - 10;
                if (translationWindow.Top + translationWindow.ActualHeight > SystemParameters.PrimaryScreenHeight)
                    translationWindow.Top = SystemParameters.PrimaryScreenHeight - translationWindow.ActualHeight - 10;
            };

            translationWindow.Closed += (s, e) =>
            {
                if (currentTranslationWindow == translationWindow)
                    currentTranslationWindow = null;
            };

            currentTranslationWindow = translationWindow;
            translationWindow.Show();
        }

        protected override void OnClosed(EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(handle, HOTKEY_ID);

            translationService.Dispose();

            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }

            base.OnClosed(e);
        }
    }
}