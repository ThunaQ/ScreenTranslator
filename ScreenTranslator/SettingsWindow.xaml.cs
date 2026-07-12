using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static ScreenTranslator.MainWindow;

namespace ScreenTranslator
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        // Language codes order for API (en, tr, ru, es, fr, it, de)
        private string[] apiLanguages = { "en", "tr", "ru", "es", "fr", "it", "de" };

        // Language codes order for Tesseract Image Reader (eng, tur, rus, spa, fra, ita, deu)
        private string[] tessLanguages = { "eng", "tur", "rus", "spa", "fra", "ita", "deu" };

        public SettingsWindow()
        {
            InitializeComponent();
            CmbShortcut.SelectedIndex = AppSettings.ShortcutSelection;
            ChkStartUp.IsChecked = AppSettings.AutoStart;

            // Reflect saved settings into the combo boxes when the window opens
            CmbApi.SelectedIndex = AppSettings.SelectedApi == "Google" ? 0 : 1;
            CmbSourceLang.SelectedIndex = Array.IndexOf(apiLanguages, AppSettings.SourceLang);
            CmbTargetLang.SelectedIndex = Array.IndexOf(apiLanguages, AppSettings.TargetLang);

            // If index is not found (-1), default to English (0) and Turkish (1)
            if (CmbSourceLang.SelectedIndex == -1) CmbSourceLang.SelectedIndex = 0;
            if (CmbTargetLang.SelectedIndex == -1) CmbTargetLang.SelectedIndex = 1;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // API Selection
            AppSettings.SelectedApi = CmbApi.SelectedIndex == 0 ? "Google" : "Yandex";

            // Source Language (Language to be read)
            int sourceSelection = CmbSourceLang.SelectedIndex;
            AppSettings.SourceLang = apiLanguages[sourceSelection];
            AppSettings.TessLang = tessLanguages[sourceSelection];

            // Target Language (Language to translate into)
            int targetSelection = CmbTargetLang.SelectedIndex;
            AppSettings.TargetLang = apiLanguages[targetSelection];

            // Get new shortcut and startup settings
            AppSettings.ShortcutSelection = CmbShortcut.SelectedIndex;
            AppSettings.AutoStart = ChkStartUp.IsChecked == true;

            // NEW: Permanently save memory to computer (JSON and Registry)
            AppSettings.Save();

            this.Close();
        }
    }
}