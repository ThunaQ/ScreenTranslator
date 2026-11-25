#nullable disable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using Size = System.Windows.Size;

namespace ScreenTranslator
{
    public enum PopupKind
    {
        Loading,
        Result,
        Error
    }

    /// <summary>
    /// Small borderless card shown next to the selected area: "Translating...", the translation, or why it failed.
    /// It never takes focus, so the app the user is working in keeps it.
    /// </summary>
    public sealed class ResultPopup : Window
    {
        private const double MinCardWidth = 280;
        private const double MaxCardWidth = 540;
        private const double ShadowMargin = 16;
        private const double Gap = 8;

        private static readonly FontFamily GlyphFont = new FontFamily("Segoe MDL2 Assets");

        private readonly Grid root;
        private readonly Border card;
        private readonly TextBlock body;
        private readonly Border copyButton;
        private readonly TextBlock copyGlyph;
        private readonly DispatcherTimer autoCloseTimer;
        private readonly DispatcherTimer copiedTimer;

        private Rect anchor;
        private string currentText = "";

        public ResultPopup()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            UseLayoutRounding = true;

            body = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
                FontSize = 15,
                LineHeight = 22,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Frozen(Color.FromRgb(0xE8, 0xEB, 0xF0)),
                Margin = new Thickness(16, 14, 8, 16)
            };

            copyButton = CreateIconButton("\uE8C8", Loc.T("popup.copy"), OnCopy, out copyGlyph);
            Border closeButton = CreateIconButton("\uE711", Loc.T("popup.close"), () => Close(), out _);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 8, 8, 0)
            };
            actions.Children.Add(copyButton);
            actions.Children.Add(closeButton);

            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(body, 0);
            Grid.SetColumn(actions, 1);
            layout.Children.Add(body);
            layout.Children.Add(actions);

            card = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Background = Frozen(Color.FromArgb(0xF5, 0x1B, 0x1F, 0x26)),
                BorderBrush = Frozen(Color.FromRgb(0x2E, 0x34, 0x3E)),
                Child = layout,
                Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 2, Opacity = 0.45, Color = Colors.Black }
            };
            card.MouseLeftButtonDown += (s, e) =>
            {
                try { DragMove(); } catch (InvalidOperationException) { }
            };

            root = new Grid { Margin = new Thickness(ShadowMargin) };
            root.Children.Add(card);
            Content = root;

            autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            autoCloseTimer.Tick += (s, e) => Close();

            copiedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.4) };
            copiedTimer.Tick += (s, e) =>
            {
                copiedTimer.Stop();
                copyGlyph.Text = "\uE8C8";
            };

            Closed += (s, e) =>
            {
                autoCloseTimer.Stop();
                copiedTimer.Stop();
            };
        }

        /// <summary>Shows or updates the popup. <paramref name="anchor"/> is the selected area in screen DIPs.</summary>
        public void Present(PopupKind kind, string text, Rect anchor)
        {
            this.anchor = anchor;
            currentText = text?.Trim() ?? "";
            autoCloseTimer.Stop();
            copiedTimer.Stop();
            copyGlyph.Text = "\uE8C8";

            body.Text = currentText;

            switch (kind)
            {
                case PopupKind.Loading:
                    body.Foreground = Frozen(Color.FromRgb(0x98, 0xA1, 0xB0));
                    card.BorderBrush = Frozen(Color.FromRgb(0x2E, 0x34, 0x3E));
                    copyButton.Visibility = Visibility.Collapsed;
                    break;

                case PopupKind.Error:
                    body.Foreground = Frozen(Color.FromRgb(0xFF, 0xB8, 0xB8));
                    card.BorderBrush = Frozen(Color.FromRgb(0xF2, 0x70, 0x70));
                    copyButton.Visibility = Visibility.Collapsed;
                    autoCloseTimer.Start();
                    break;

                default:
                    body.Foreground = Frozen(Color.FromRgb(0xE8, 0xEB, 0xF0));
                    card.BorderBrush = Frozen(Color.FromRgb(0x2E, 0x34, 0x3E));
                    copyButton.Visibility = Visibility.Visible;
                    break;
            }

            card.Width = Math.Min(MaxCardWidth, Math.Max(MinCardWidth, anchor.Width));
            Reposition();

            if (!IsVisible)
                Show();
        }

        private void Reposition()
        {
            root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Size size = root.DesiredSize;

            double minX = SystemParameters.VirtualScreenLeft;
            double minY = SystemParameters.VirtualScreenTop;
            double maxX = minX + SystemParameters.VirtualScreenWidth;
            double maxY = minY + SystemParameters.VirtualScreenHeight;

            // Prefer below the selection; flip above when it would run off the screen.
            double left = anchor.Left - ShadowMargin;
            double top = anchor.Bottom + Gap - ShadowMargin;

            if (top + size.Height > maxY)
                top = anchor.Top - Gap - size.Height + ShadowMargin;

            if (left + size.Width > maxX) left = maxX - size.Width;
            if (left < minX) left = minX;
            if (top + size.Height > maxY) top = maxY - size.Height;
            if (top < minY) top = minY;

            Left = left;
            Top = top;
        }

        private void OnCopy()
        {
            try
            {
                System.Windows.Clipboard.SetText(currentText);
            }
            catch
            {
                return; // clipboard briefly locked by another app
            }

            copyGlyph.Text = "\uE73E"; // check mark
            copiedTimer.Stop();
            copiedTimer.Start();
        }

        private static Border CreateIconButton(string glyph, string tooltip, Action onClick, out TextBlock glyphBlock)
        {
            var normal = Frozen(Color.FromRgb(0x98, 0xA1, 0xB0));
            var hot = Frozen(Color.FromRgb(0xE8, 0xEB, 0xF0));

            TextBlock text = new TextBlock
            {
                Text = glyph,
                FontFamily = GlyphFont,
                FontSize = 12,
                Foreground = normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var button = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                Background = System.Windows.Media.Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Child = text
            };

            button.MouseEnter += (s, e) =>
            {
                button.Background = Frozen(Color.FromRgb(0x2B, 0x31, 0x3B));
                text.Foreground = hot;
            };
            button.MouseLeave += (s, e) =>
            {
                button.Background = System.Windows.Media.Brushes.Transparent;
                text.Foreground = normal;
            };
            button.MouseLeftButtonDown += (s, e) => e.Handled = true; // don't start dragging the card
            button.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                onClick();
            };

            glyphBlock = text;
            return button;
        }

        private static SolidColorBrush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}