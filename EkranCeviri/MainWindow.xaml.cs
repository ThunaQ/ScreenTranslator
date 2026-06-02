using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Drawing;
using System.Drawing.Drawing2D;
using Tesseract;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using GTranslate.Translators;
using System.Text.RegularExpressions;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using System.Diagnostics;

namespace EkranCeviri
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;
        private System.Windows.Forms.NotifyIcon tepsiSimgesi;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CTRL = 0x0002;
        private const uint VK_X = 0x58;

        private System.Windows.Point baslangicNoktasi;
        private bool cizimYapiliyor = false;

        public MainWindow()
        {
            InitializeComponent();

            // --- SAĞ ALT SİMGE (SYSTEM TRAY) AYARLARI ---
            tepsiSimgesi = new System.Windows.Forms.NotifyIcon();

            // ESKİ KOD BUYDU: tepsiSimgesi.Icon = System.Drawing.SystemIcons.Application;
            // YENİ KOD (Kendi ikonunun tam adını yazmalısın, örneğin "benim_ikonum.ico"):
            tepsiSimgesi.Icon = new System.Drawing.Icon("TR.ico");

            // Sağ alt simge yazısı
            tepsiSimgesi.Text = "ScreenTranslator - Running in background";
            tepsiSimgesi.Visible = true;

            // --- SAĞ TIK MENÜSÜ ---
            System.Windows.Forms.ContextMenuStrip sagTikMenusu = new System.Windows.Forms.ContextMenuStrip();

            // 1. Seçenek: Settings (Ayarlar yerine)
            System.Windows.Forms.ToolStripMenuItem ayarlarItem = new System.Windows.Forms.ToolStripMenuItem("Settings"); ayarlarItem.Click += (s, args) =>
            {
                // WPF'in ana arayüz motoruna güvenli şekilde bağlanıyoruz
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SettingsWindow ayarlarPenceresi = new SettingsWindow();

                    // Pencerenin diğer tüm pencerelerin üstünde açılmasını garanti ediyoruz
                    ayarlarPenceresi.Topmost = true;

                    ayarlarPenceresi.ShowDialog();
                });
            };

            // 2. Seçenek: Exit (Çıkış yerine)
            System.Windows.Forms.ToolStripMenuItem cikisItem = new System.Windows.Forms.ToolStripMenuItem("Exit"); cikisItem.Click += (s, args) =>
            {
                System.Windows.Application.Current.Shutdown();
            };

            // Menüye butonları ekle
            sagTikMenusu.Items.Add(ayarlarItem);
            sagTikMenusu.Items.Add(cikisItem);

            // Menüyü simgeye bağla
            tepsiSimgesi.ContextMenuStrip = sagTikMenusu;
        }
        public static class UygulamaAyarlari
        {
            public static string SeciliApi = "Google";
            public static string KaynakDil = "en";
            public static string HedefDil = "tr";
            public static string TesseractDil = "eng";

            // YENİ: Kısayol Tuşu ve Windows ile Başlama Ayarları
            public static int KisayolSecimi = 0; // 0: Ctrl+Alt+X, 1: Ctrl+Shift+C, 2: Alt+Z
            public static bool OtomatikBaslat = false;

            private static string ayarDosyasi = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ayarlar.json");

            public static void Kaydet()
            {
                // 1. Ayarları JSON dosyasına yaz (Uygulama kapatılsa da unutmaz)
                var ayarlar = new { SeciliApi, KaynakDil, HedefDil, TesseractDil, KisayolSecimi, OtomatikBaslat };
                File.WriteAllText(ayarDosyasi, JsonSerializer.Serialize(ayarlar));

                // 2. Windows başlangıcına ekle veya çıkar (ÇİLEK MEVZU)
                RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (OtomatikBaslat)
                    rk.SetValue("ScreenTranslator", Process.GetCurrentProcess().MainModule.FileName);
                else
                    rk.DeleteValue("ScreenTranslator", false);
            }

            public static bool Yukle()
            {
                if (File.Exists(ayarDosyasi))
                {
                    string json = File.ReadAllText(ayarDosyasi);
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("SeciliApi", out var p)) SeciliApi = p.GetString();
                        if (root.TryGetProperty("KaynakDil", out p)) KaynakDil = p.GetString();
                        if (root.TryGetProperty("HedefDil", out p)) HedefDil = p.GetString();
                        if (root.TryGetProperty("TesseractDil", out p)) TesseractDil = p.GetString();
                        if (root.TryGetProperty("KisayolSecimi", out p)) KisayolSecimi = p.GetInt32();
                        if (root.TryGetProperty("OtomatikBaslat", out p)) OtomatikBaslat = p.GetBoolean();
                    }
                    return true; // Ayar dosyası bulundu, uygulama daha önce açılmış
                }
                return false; // Dosya yok, demek ki İLK KEZ açılıyor
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Hide();

            // Uygulama başlarken hafızayı yüklemeye çalışır
            bool dahaOnceAcildi = UygulamaAyarlari.Yukle();

            // Eğer yükleyemezse (ilk defa açılıyorsa) ayarlar menüsünü ekrana zorla getirir
            if (!dahaOnceAcildi)
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SettingsWindow ayarlarPenceresi = new SettingsWindow();
                    ayarlarPenceresi.Topmost = true;
                    ayarlarPenceresi.ShowDialog();
                });
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr handle = new WindowInteropHelper(this).Handle;

            // Hafızadan tuş seçimini alıyoruz
            uint fsModifiers = MOD_CTRL | MOD_ALT;
            uint vk = VK_X; // Varsayılan: Ctrl + Alt + X

            if (UygulamaAyarlari.KisayolSecimi == 1)
            {
                fsModifiers = MOD_CTRL | 0x0004; // 0x0004 = Shift tuşunun Windows kodu
                vk = 0x43; // C harfi (Ctrl + Shift + C)
            }
            else if (UygulamaAyarlari.KisayolSecimi == 2)
            {
                fsModifiers = MOD_ALT;
                vk = 0x5A; // Z harfi (Alt + Z)
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
                SecimKutusu.Visibility = Visibility.Collapsed;
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void CizimAlani_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            cizimYapiliyor = true;
            baslangicNoktasi = e.GetPosition(CizimAlani);

            Canvas.SetLeft(SecimKutusu, baslangicNoktasi.X);
            Canvas.SetTop(SecimKutusu, baslangicNoktasi.Y);
            SecimKutusu.Width = 0;
            SecimKutusu.Height = 0;
            SecimKutusu.Visibility = Visibility.Visible;

            CizimAlani.CaptureMouse();
        }

        private void CizimAlani_MouseMove(object sender, MouseEventArgs e)
        {
            if (!cizimYapiliyor) return;

            System.Windows.Point guncelNokta = e.GetPosition(CizimAlani);

            double x = Math.Min(guncelNokta.X, baslangicNoktasi.X);
            double y = Math.Min(guncelNokta.Y, baslangicNoktasi.Y);
            double width = Math.Max(guncelNokta.X, baslangicNoktasi.X) - x;
            double height = Math.Max(guncelNokta.Y, baslangicNoktasi.Y) - y;

            Canvas.SetLeft(SecimKutusu, x);
            Canvas.SetTop(SecimKutusu, y);
            SecimKutusu.Width = width;
            SecimKutusu.Height = height;
        }

        private async void CizimAlani_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            cizimYapiliyor = false;
            CizimAlani.ReleaseMouseCapture();

            PresentationSource source = PresentationSource.FromVisual(this);
            double dpiX = 1.0; double dpiY = 1.0;
            if (source != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            this.Hide();

            double wpfX = Canvas.GetLeft(SecimKutusu);
            double wpfY = Canvas.GetTop(SecimKutusu);
            double wpfWidth = SecimKutusu.Width;
            double wpfHeight = SecimKutusu.Height;

            if (wpfWidth > 5 && wpfHeight > 5)
            {
                int gercekX = (int)(wpfX * dpiX);
                int gercekY = (int)(wpfY * dpiY);
                int gercekWidth = (int)(wpfWidth * dpiX);
                int gercekHeight = (int)(wpfHeight * dpiY);

                using (Bitmap bmp = new Bitmap(gercekWidth, gercekHeight))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(gercekX, gercekY, 0, 0, bmp.Size);
                    }

                    int olcek = 3;
                    using (Bitmap buyukBmp = new Bitmap(gercekWidth * olcek, gercekHeight * olcek))
                    {
                        using (Graphics gBuyuk = Graphics.FromImage(buyukBmp))
                        {
                            gBuyuk.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            gBuyuk.DrawImage(bmp, 0, 0, buyukBmp.Width, buyukBmp.Height);
                        }

                        // --- YENİ: KARANLIK TEMA DÜZELTMESİ (Renkleri Tersine Çevir) ---
                        using (Bitmap islenmisBmp = new Bitmap(buyukBmp.Width, buyukBmp.Height))
                        {
                            using (Graphics gIslem = Graphics.FromImage(islenmisBmp))
                            {
                                // Renkleri tersine çeviren matris (Siyahı beyaz, beyazı siyah yapar)
                                System.Drawing.Imaging.ColorMatrix colorMatrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
                                {
                new float[] {-1, 0, 0, 0, 0},
                new float[] {0, -1, 0, 0, 0},
                new float[] {0, 0, -1, 0, 0},
                new float[] {0, 0, 0, 1, 0},
                new float[] {1, 1, 1, 0, 1}
                                });

                                System.Drawing.Imaging.ImageAttributes attributes = new System.Drawing.Imaging.ImageAttributes();
                                attributes.SetColorMatrix(colorMatrix);

                                gIslem.DrawImage(buyukBmp, new Rectangle(0, 0, islenmisBmp.Width, islenmisBmp.Height),
                                                 0, 0, buyukBmp.Width, buyukBmp.Height, GraphicsUnit.Pixel, attributes);
                            }

                            try
                            {
                                string tessDataYolu = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                                byte[] resimBytes;
                                using (var stream = new System.IO.MemoryStream())
                                {
                                    // Artık tersine çevrilmiş, Tesseract'ın sevdiği resmi gönderiyoruz
                                    islenmisBmp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                                    resimBytes = stream.ToArray();
                                }

                                using (var engine = new TesseractEngine(tessDataYolu, UygulamaAyarlari.TesseractDil, EngineMode.LstmOnly))
                                {
                                    using (var img = Pix.LoadFromMemory(resimBytes))
                                    {
                                        // SingleBlock yerine Auto (PSM 3) kullanıyoruz. Karışık yapıları daha iyi çözer.
                                        using (var page = engine.Process(img, PageSegMode.Auto))
                                        {
                                            string okunanMetin = page.GetText();

                                            if (!string.IsNullOrWhiteSpace(okunanMetin))
                                            {
                                                // 1. RUSÇA HARFLERE (Kiril Alfabesi) İZİN VERİYORUZ!
                                                // а-яА-ЯёЁ kısmını ekledik ki Tesseract'ın okuduğu Rusça harfler silinmesin.
                                                okunanMetin = Regex.Replace(okunanMetin, @"[^a-zA-Z0-9\s.,?!'üğişçöÜĞİŞÇÖа-яА-ЯёЁ-]", "");

                                                // 2. Satır sonunda heceleme/tire varsa birleştir (örn: "trans-\nlation" -> "translation")
                                                okunanMetin = okunanMetin.Replace("-\n", "").Replace("-\r\n", "");

                                                // 3. AKILLI SATIR BİRLEŞTİRME (Cümle Bölünmesini Engeller)
                                                // Tekli Enter'ları (cümle kayması) boşluğa çevirir, ama çift Enter'ları (GitHub listesi, yeni paragraf) korur.
                                                okunanMetin = Regex.Replace(okunanMetin, @"(?<!\r?\n)\r?\n(?!\r?\n)", " ");

                                                // 4. Fazladan oluşan yan yana boşlukları temizle
                                                okunanMetin = Regex.Replace(okunanMetin, @"[ \t]+", " ").Trim();

                                                if (okunanMetin.Length > 3)
                                                {
                                                    string turkceCeviri = "";

                                                    // Ayarlardan hangi API seçildiyse onu kullan
                                                    if (UygulamaAyarlari.SeciliApi == "Google")
                                                    {
                                                        var googleCevirmen = new GoogleTranslator();
                                                        var sonuc = await googleCevirmen.TranslateAsync(okunanMetin, UygulamaAyarlari.HedefDil, UygulamaAyarlari.KaynakDil);
                                                        turkceCeviri = sonuc.Translation;
                                                    }
                                                    else
                                                    {
                                                        var yandexCevirmen = new YandexTranslator();
                                                        var sonuc = await yandexCevirmen.TranslateAsync(okunanMetin, UygulamaAyarlari.HedefDil, UygulamaAyarlari.KaynakDil);
                                                        turkceCeviri = sonuc.Translation;
                                                    }

                                                    GosterSikCeviriKutusu(turkceCeviri, wpfX, wpfY + wpfHeight + 10, wpfWidth);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                // Sessizce yut
                            }
                        }
                    }
                }
            }
        }

        private async Task<string> MetniCevirAsync(string metin, string kaynakDil, string hedefDil)
        {
            try
            {
                // Yandex'in oyun/hikaye çevirileri Türkçede çok daha doğaldır.
                // İstersen buraya YandexTranslator yerine BingTranslator da yazabilirsin.
                var yandexCevirmen = new YandexTranslator();

                var sonuc = await yandexCevirmen.TranslateAsync(metin, hedefDil, kaynakDil);

                return sonuc.Translation;
            }
            catch (Exception ex)
            {
                return "Çeviri servisine bağlanılamadı: " + ex.Message;
            }
        }

        // --- YEPYENİ, SÜRÜKLENEBİLİR ŞIK KUTU ---
        private void GosterSikCeviriKutusu(string ceviri, double solX, double ustY, double cizimGenisligi)
        {
            Window ceviriPenceresi = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                SizeToContent = SizeToContent.Height,
                Width = Math.Max(250, cizimGenisligi),
                Left = solX,
                Top = ustY
            };

            Border cerceve = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(240, 25, 25, 25)),
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 60, 60, 60)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand // Kutuya gelince el simgesi çıkar
            };

            // YENİ: Artık üstteki kalın çubuk yok. Kutunun neresinden tutarsan tut sürüklenebilir!
            cerceve.MouseLeftButtonDown += (s, e) => { ceviriPenceresi.DragMove(); };

            Grid anaGrid = new Grid();

            // Kapatma Butonu (X) - Direk sağ üste zarifçe yerleştirildi
            TextBlock btnKapat = new TextBlock
            {
                Text = "✕",
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 5, 8, 0),
                Cursor = Cursors.Arrow // X'in üzerindeyken normal imleç
            };
            btnKapat.MouseEnter += (s, e) => btnKapat.Foreground = System.Windows.Media.Brushes.Red;
            btnKapat.MouseLeave += (s, e) => btnKapat.Foreground = System.Windows.Media.Brushes.Gray;
            btnKapat.MouseDown += (s, e) => ceviriPenceresi.Close();

            // Çeviri Metni
            TextBlock txtCeviri = new TextBlock
            {
                Text = ceviri.Trim(),
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Medium,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(15, 20, 15, 15) // Üstten biraz boşluk bıraktık ki X ile çakışmasın
            };

            anaGrid.Children.Add(txtCeviri);
            anaGrid.Children.Add(btnKapat); // X butonunu en üste ekle

            cerceve.Child = anaGrid;
            ceviriPenceresi.Content = cerceve;

            // Ekran dışına taşma kontrolü
            ceviriPenceresi.Loaded += (s, e) =>
            {
                if (ceviriPenceresi.Left + ceviriPenceresi.ActualWidth > SystemParameters.PrimaryScreenWidth)
                    ceviriPenceresi.Left = SystemParameters.PrimaryScreenWidth - ceviriPenceresi.ActualWidth - 10;
                if (ceviriPenceresi.Top + ceviriPenceresi.ActualHeight > SystemParameters.PrimaryScreenHeight)
                    ceviriPenceresi.Top = SystemParameters.PrimaryScreenHeight - ceviriPenceresi.ActualHeight - 10;
            };

            ceviriPenceresi.Show();
        }

        protected override void OnClosed(EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(handle, HOTKEY_ID);

            // Uygulama kapanırken simgeyi yok et
            if (tepsiSimgesi != null)
            {
                tepsiSimgesi.Visible = false;
                tepsiSimgesi.Dispose();
            }

            base.OnClosed(e);
        }
    }
}