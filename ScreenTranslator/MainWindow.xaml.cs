#nullable disable
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingRectangle = System.Drawing.Rectangle;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace ScreenTranslator
{
    public partial class MainWindow : Window
    {
        private const int HotkeyId = 9000;

        private readonly TranslationService translationService = new TranslationService();
        private readonly bool needsFirstRunSetup;

        private System.Windows.Forms.NotifyIcon trayIcon;
        private IntPtr windowHandle = IntPtr.Zero;
        private bool hotkeyRegistered;

        private DrawingBitmap frozenScreen;
        private bool overlayActive;
        private bool isDrawing;
        private bool startingCapture;
        private DateTime overlayShownAt = DateTime.MinValue;
        private Point startPoint;

        private ResultPopup popup;
        private SettingsWindow settingsWindow;
        private HistoryWindow historyWindow;
        private int requestCounter;

        public MainWindow()
        {
            // Settings must be loaded before the hotkey is registered (OnSourceInitialized runs later).
            needsFirstRunSetup = !AppSettings.Load();

            InitializeComponent();
            InitializeTrayIcon();

            // The tray menu is built in code, so it rebuilds itself when the interface language changes.
            Loc.LanguageChanged += BuildTrayMenu;
        }

        // ------------------------------------------------------------------
        // Tray icon and windows
        // ------------------------------------------------------------------

        private void InitializeTrayIcon()
        {
            System.Drawing.Icon icon;
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                icon = exePath != null
                    ? System.Drawing.Icon.ExtractAssociatedIcon(exePath)
                    : System.Drawing.SystemIcons.Application;
            }
            catch
            {
                icon = System.Drawing.SystemIcons.Application;
            }

            trayIcon = new System.Windows.Forms.NotifyIcon { Icon = icon, Visible = true };
            trayIcon.DoubleClick += (s, e) => _ = BeginCaptureAsync(250);

            BuildTrayMenu();
            UpdateTrayText();
        }

        /// <summary>(Re)creates the tray menu so its texts follow the interface language.</summary>
        private void BuildTrayMenu()
        {
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add(Loc.T("tray.translate"), null, (s, e) => _ = BeginCaptureAsync(250));
            menu.Items.Add(Loc.T("tray.history"), null, (s, e) => OpenHistory());
            menu.Items.Add(Loc.T("tray.settings"), null, (s, e) => OpenSettings());
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(Loc.T("tray.exit"), null, (s, e) => System.Windows.Application.Current.Shutdown());

            System.Windows.Forms.ContextMenuStrip old = trayIcon.ContextMenuStrip;
            trayIcon.ContextMenuStrip = menu;
            old?.Dispose();
        }

        private void UpdateTrayText()
        {
            if (trayIcon != null)
                trayIcon.Text = "ScreenTranslator (" + AppSettings.HotkeyDisplay + ")";
        }

        private void OpenSettings()
        {
            if (settingsWindow != null)
            {
                settingsWindow.Activate();
                return;
            }

            if (overlayActive)
                HideOverlay();

            settingsWindow = new SettingsWindow { Topmost = true };
            bool? saved = settingsWindow.ShowDialog();
            settingsWindow = null;

            // A modal dialog can leave this hidden window disabled; the overlay needs it enabled.
            IsEnabled = true;

            if (saved == true)
            {
                RegisterHotkey();
                UpdateTrayText();
            }
        }

        private void OpenHistory()
        {
            if (historyWindow != null)
            {
                historyWindow.Activate();
                return;
            }

            historyWindow = new HistoryWindow { Topmost = true };
            historyWindow.Closed += (s, e) => historyWindow = null;
            historyWindow.Show();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Hide();

            if (needsFirstRunSetup)
                Dispatcher.BeginInvoke(new Action(OpenSettings));
        }

        // ------------------------------------------------------------------
        // Global hotkey
        // ------------------------------------------------------------------

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            windowHandle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(windowHandle)?.AddHook(HwndHook);

            RegisterHotkey();
        }

        private bool RegisterHotkey()
        {
            if (windowHandle == IntPtr.Zero)
                return false;

            if (hotkeyRegistered)
            {
                NativeMethods.UnregisterHotKey(windowHandle, HotkeyId);
                hotkeyRegistered = false;
            }

            hotkeyRegistered = NativeMethods.RegisterHotKey(
                windowHandle, HotkeyId, AppSettings.HotkeyModifiers | NativeMethods.ModNoRepeat, AppSettings.HotkeyKey);

            AppLog.Write("Hotkey " + AppSettings.HotkeyDisplay + " registered: " + hotkeyRegistered);

            if (!hotkeyRegistered)
            {
                trayIcon?.ShowBalloonTip(5000, "ScreenTranslator",
                    Loc.T("balloon.hotkeyused", AppSettings.HotkeyDisplay),
                    System.Windows.Forms.ToolTipIcon.Warning);
            }

            return hotkeyRegistered;
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                handled = true;
                AppLog.Write("Hotkey pressed");
                _ = BeginCaptureAsync();
            }

            return IntPtr.Zero;
        }

        // ------------------------------------------------------------------
        // Capture overlay
        // ------------------------------------------------------------------

        private async Task BeginCaptureAsync(int initialDelayMs = 0)
        {
            if (settingsWindow != null || startingCapture)
                return; // the modal settings dialog would block the overlay

            if (overlayActive)
            {
                HideOverlay(); // pressing the shortcut again cancels
                return;
            }

            startingCapture = true;
            requestCounter++; // invalidates any translation still in flight

            try
            {
                bool hadPopup = popup != null;
                ClosePopup();

                int delay = Math.Max(initialDelayMs, hadPopup ? 80 : 0);
                if (delay > 0)
                    await Task.Delay(delay); // let the previous popup / tray menu disappear from the screen

                frozenScreen?.Dispose();
                frozenScreen = CaptureVirtualScreen();
                ShowOverlay();
                AppLog.Write("Overlay shown");
            }
            catch (Exception ex)
            {
                AppLog.Write("Could not start capture: " + ex);
                HideOverlay();

                trayIcon?.ShowBalloonTip(5000, "ScreenTranslator",
                    Loc.T("err.capture") + " (" + ex.GetType().Name + ")",
                    System.Windows.Forms.ToolTipIcon.Error);
            }
            finally
            {
                startingCapture = false;
            }
        }

        private static DrawingBitmap CaptureVirtualScreen()
        {
            System.Drawing.Rectangle bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
            var bitmap = new DrawingBitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                // Plain copy on purpose: combining CaptureBlt with SourceCopy makes CopyFromScreen throw on some setups.
                g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
            }

            return bitmap;
        }

        private static BitmapSource ToBitmapSource(DrawingBitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                NativeMethods.DeleteObject(hBitmap);
            }
        }

        private void ShowOverlay()
        {
            IsEnabled = true;

            // One window spanning every monitor.
            WindowState = WindowState.Normal;
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            FrozenImage.Source = ToBitmapSource(frozenScreen);
            SelectionBox.Visibility = Visibility.Collapsed;
            DimHole.Rect = new Rect(0, 0, 0, 0);
            HintPill.Visibility = Visibility.Visible;
            isDrawing = false;
            overlayActive = true;
            overlayShownAt = DateTime.UtcNow;

            Show();
            Activate();
            Focus();
        }

        private void HideOverlay()
        {
            overlayActive = false;
            isDrawing = false;

            if (CanvasArea.IsMouseCaptured)
                CanvasArea.ReleaseMouseCapture();

            Hide();
            FrozenImage.Source = null;

            frozenScreen?.Dispose();
            frozenScreen = null;
        }

        private void CanvasArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DimFull.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && overlayActive)
                HideOverlay();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            // Alt+Tab or a notification stole focus: don't leave a frozen screen lying around.
            // Ignore the first moments after showing, when Windows may still be shuffling activation.
            if (overlayActive && !isDrawing && (DateTime.UtcNow - overlayShownAt).TotalMilliseconds > 600)
                HideOverlay();
        }

        private void CanvasArea_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (overlayActive)
                HideOverlay();
        }

        private void CanvasArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDrawing = true;
            startPoint = e.GetPosition(CanvasArea);
            HintPill.Visibility = Visibility.Collapsed;

            UpdateSelection(startPoint, startPoint);
            SelectionBox.Visibility = Visibility.Visible;

            CanvasArea.CaptureMouse();
        }

        private void CanvasArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDrawing)
                return;

            UpdateSelection(startPoint, e.GetPosition(CanvasArea));
        }

        private void UpdateSelection(Point a, Point b)
        {
            double x = Math.Min(a.X, b.X);
            double y = Math.Min(a.Y, b.Y);
            double width = Math.Abs(a.X - b.X);
            double height = Math.Abs(a.Y - b.Y);

            Canvas.SetLeft(SelectionBox, x);
            Canvas.SetTop(SelectionBox, y);
            SelectionBox.Width = width;
            SelectionBox.Height = height;
            DimHole.Rect = new Rect(x, y, width, height);
        }

        private void CanvasArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDrawing)
                return;

            isDrawing = false;
            CanvasArea.ReleaseMouseCapture();

            double x = Canvas.GetLeft(SelectionBox);
            double y = Canvas.GetTop(SelectionBox);
            double width = SelectionBox.Width;
            double height = SelectionBox.Height;

            if (width < 8 || height < 8)
            {
                HideOverlay(); // a click or a tiny drag cancels
                return;
            }

            DrawingBitmap crop = CropFrozen(x, y, width, height);
            Rect anchor = new Rect(Left + x, Top + y, width, height); // selection in screen DIPs

            HideOverlay();

            if (crop != null)
                _ = ProcessCaptureAsync(crop, anchor);
        }

        private DrawingBitmap CropFrozen(double x, double y, double width, double height)
        {
            if (frozenScreen == null || CanvasArea.ActualWidth <= 0 || CanvasArea.ActualHeight <= 0)
                return null;

            // Map canvas DIPs to bitmap pixels; correct at any DPI scale.
            double scaleX = frozenScreen.Width / CanvasArea.ActualWidth;
            double scaleY = frozenScreen.Height / CanvasArea.ActualHeight;

            var wanted = new DrawingRectangle(
                (int)Math.Floor(x * scaleX), (int)Math.Floor(y * scaleY),
                (int)Math.Ceiling(width * scaleX), (int)Math.Ceiling(height * scaleY));

            DrawingRectangle area = DrawingRectangle.Intersect(
                new DrawingRectangle(0, 0, frozenScreen.Width, frozenScreen.Height), wanted);

            if (area.Width < 4 || area.Height < 4)
                return null;

            return frozenScreen.Clone(area, frozenScreen.PixelFormat);
        }

        // ------------------------------------------------------------------
        // Translation
        // ------------------------------------------------------------------

        private async Task ProcessCaptureAsync(DrawingBitmap crop, Rect anchor)
        {
            int requestId = ++requestCounter;
            ShowPopup(PopupKind.Loading, Loc.T("popup.translating"), anchor);

            try
            {
                using (crop)
                {
                    TranslationResult result = await translationService.TranslateCaptureAsync(
                        crop, AppSettings.SourceLang, AppSettings.TargetLang, AppSettings.TessLang,
                        AppSettings.SelectedApi, AppSettings.AutoDetectSource);

                    if (requestId != requestCounter)
                        return; // a newer capture replaced this one

                    AppLog.Write(result.Status + " via " + result.EngineUsed + " | OCR: " + result.ExtractedText.Replace("\n", " | "));

                    if (result.HasContent)
                    {
                        TranslationHistory.Add(result.ExtractedText, result.TranslatedText);
                        ShowPopup(PopupKind.Result, result.TranslatedText, anchor);
                    }
                    else
                    {
                        ShowPopup(PopupKind.Error, result.Message, anchor);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("Capture/translate failed: " + ex);

                if (requestId == requestCounter)
                    ShowPopup(PopupKind.Error, Loc.T("err.generic"), anchor);
            }
        }

        private void ShowPopup(PopupKind kind, string text, Rect anchor)
        {
            if (popup == null)
            {
                popup = new ResultPopup();
                popup.Closed += (s, e) =>
                {
                    if (ReferenceEquals(s, popup))
                        popup = null;
                };
            }

            popup.Present(kind, text, anchor);
        }

        private void ClosePopup()
        {
            ResultPopup old = popup;
            popup = null;
            old?.Close();
        }

        // ------------------------------------------------------------------
        // Shutdown
        // ------------------------------------------------------------------

        protected override void OnClosed(EventArgs e)
        {
            Loc.LanguageChanged -= BuildTrayMenu;

            if (hotkeyRegistered)
                NativeMethods.UnregisterHotKey(windowHandle, HotkeyId);

            frozenScreen?.Dispose();
            popup?.Close();
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