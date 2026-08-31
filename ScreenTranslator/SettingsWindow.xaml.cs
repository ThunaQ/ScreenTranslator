using System;
using System.Windows;
using static ScreenTranslator.MainWindow;

namespace ScreenTranslator
{
    public partial class SettingsWindow : Window
    {
        private readonly string[] apiLanguages = { "en", "tr", "ru", "es", "fr", "it", "de" };
        private readonly string[] tessLanguages = { "eng", "tur", "rus", "spa", "fra", "ita", "deu" };

        public SettingsWindow()
        {
            InitializeComponent();
            CmbShortcut.SelectedIndex = AppSettings.ShortcutSelection;
            ChkStartUp.IsChecked = AppSettings.AutoStart;
            ChkAutoDetect.IsChecked = AppSettings.AutoDetectSource;

            CmbApi.SelectedIndex = AppSettings.SelectedApi == "Google" ? 0 : 1;
            CmbSourceLang.SelectedIndex = Array.IndexOf(apiLanguages, AppSettings.SourceLang);
            CmbTargetLang.SelectedIndex = Array.IndexOf(apiLanguages, AppSettings.TargetLang);

            if (CmbSourceLang.SelectedIndex == -1) CmbSourceLang.SelectedIndex = 0;
            if (CmbTargetLang.SelectedIndex == -1) CmbTargetLang.SelectedIndex = 1;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.SelectedApi = CmbApi.SelectedIndex == 0 ? "Google" : "Yandex";

            int sourceSelection = CmbSourceLang.SelectedIndex;
            AppSettings.SourceLang = apiLanguages[sourceSelection];
            AppSettings.TessLang = tessLanguages[sourceSelection];

            int targetSelection = CmbTargetLang.SelectedIndex;
            AppSettings.TargetLang = apiLanguages[targetSelection];

            AppSettings.ShortcutSelection = CmbShortcut.SelectedIndex;
            AppSettings.AutoStart = ChkStartUp.IsChecked == true;
            AppSettings.AutoDetectSource = ChkAutoDetect.IsChecked == true;

            AppSettings.Save();
            this.Close();
        }
    }
}