using System;
using System.Windows;

namespace ScreenTranslator
{
    public partial class HistoryWindow : Window
    {
        public HistoryWindow()
        {
            InitializeComponent();
            SourceInitialized += (s, e) => NativeMethods.UseDarkTitleBar(this);

            TranslationHistory.Changed += OnHistoryChanged;
            Closed += (s, e) => TranslationHistory.Changed -= OnHistoryChanged;

            Refresh();
        }

        private void OnHistoryChanged()
        {
            Dispatcher.BeginInvoke(new Action(Refresh));
        }

        private void Refresh()
        {
            var entries = TranslationHistory.Entries;
            HistoryList.ItemsSource = entries;
            EmptyState.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            BtnClear.IsEnabled = entries.Count > 0;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            TranslationHistory.Clear(); // raises Changed, which refreshes the list
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string text && text.Length > 0)
            {
                try
                {
                    System.Windows.Clipboard.SetText(text);
                }
                catch
                {
                    // clipboard briefly locked by another app
                }
            }
        }
    }
}