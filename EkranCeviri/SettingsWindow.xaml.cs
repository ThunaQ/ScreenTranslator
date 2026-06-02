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
using static EkranCeviri.MainWindow;

namespace EkranCeviri
{
    /// <summary>
    /// SettingsWindow.xaml etkileşim mantığı
    /// </summary>
    public partial class SettingsWindow : Window
    {
        // API için dil kodları sırası (en, tr, ru, es, fr, it, de)
        private string[] apiDiller = { "en", "tr", "ru", "es", "fr", "it", "de" };

        // Tesseract Görüntü Okuyucu için dil kodları sırası (eng, tur, rus, spa, fra, ita, deu)
        private string[] tessDiller = { "eng", "tur", "rus", "spa", "fra", "ita", "deu" };

        public SettingsWindow()
        {
            InitializeComponent();
            CmbKisayol.SelectedIndex = UygulamaAyarlari.KisayolSecimi;
            ChkBaslangic.IsChecked = UygulamaAyarlari.OtomatikBaslat;

            // Pencere açıldığında hafızadaki ayarları kutulara yansıt
            CmbApi.SelectedIndex = UygulamaAyarlari.SeciliApi == "Google" ? 0 : 1;
            CmbKaynakDil.SelectedIndex = Array.IndexOf(apiDiller, UygulamaAyarlari.KaynakDil);
            CmbHedefDil.SelectedIndex = Array.IndexOf(apiDiller, UygulamaAyarlari.HedefDil);

            // Eğer index bulamazsa (-1) varsayılan olarak İngilizce (0) ve Türkçe (1) seçsin
            if (CmbKaynakDil.SelectedIndex == -1) CmbKaynakDil.SelectedIndex = 0;
            if (CmbHedefDil.SelectedIndex == -1) CmbHedefDil.SelectedIndex = 1;
        }

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            // API Seçimi
            UygulamaAyarlari.SeciliApi = CmbApi.SelectedIndex == 0 ? "Google" : "Yandex";

            // Kaynak Dil (Okunacak Dil)
            int kaynakSecim = CmbKaynakDil.SelectedIndex;
            UygulamaAyarlari.KaynakDil = apiDiller[kaynakSecim];
            UygulamaAyarlari.TesseractDil = tessDiller[kaynakSecim];

            // Hedef Dil (Çevrilecek Dil)
            int hedefSecim = CmbHedefDil.SelectedIndex;
            UygulamaAyarlari.HedefDil = apiDiller[hedefSecim];
            // Yeni tuş ve başlangıç ayarlarını al
            UygulamaAyarlari.KisayolSecimi = CmbKisayol.SelectedIndex;
            UygulamaAyarlari.OtomatikBaslat = ChkBaslangic.IsChecked == true;

            // YENİ: Hafızayı bilgisayara (JSON ve Registry) kalıcı olarak kaydet
            UygulamaAyarlari.Kaydet();


            this.Close();

            this.Close();
        }
    }
}
