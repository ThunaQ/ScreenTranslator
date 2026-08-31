using System.Windows;

namespace ScreenTranslator
{
    public partial class HistoryWindow : Window
    {
        public HistoryWindow()
        {
            InitializeComponent();
            HistoryList.ItemsSource = TranslationHistory.Entries;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            TranslationHistory.Clear();
            HistoryList.ItemsSource = null;
            HistoryList.ItemsSource = TranslationHistory.Entries;
        }
    }
}