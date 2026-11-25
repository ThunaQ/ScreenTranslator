#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Markup;

namespace ScreenTranslator
{
    public sealed class UiLanguage
    {
        public UiLanguage(string code, string name)
        {
            Code = code;
            Name = name;
        }

        public string Code { get; }

        /// <summary>Shown in its own language, so it can always be found.</summary>
        public string Name { get; }
    }

    /// <summary>
    /// Interface strings. In XAML use {local:Loc some.key} (updates live when the language changes);
    /// in code use Loc.T("some.key", args). Missing keys fall back to English.
    /// To add a language: add it to Languages and add a dictionary to Strings.
    /// </summary>
    public static class Loc
    {
        public static readonly IReadOnlyList<UiLanguage> Languages = new[]
        {
            new UiLanguage("en", "English"),
            new UiLanguage("tr", "Türkçe"),
            new UiLanguage("ru", "Русский"),
            new UiLanguage("es", "Español"),
            new UiLanguage("fr", "Français"),
            new UiLanguage("it", "Italiano"),
            new UiLanguage("de", "Deutsch"),
        };

        private static string current = DetectDefault();

        /// <summary>Raised on the UI thread after the interface language changed.</summary>
        public static event Action LanguageChanged;

        public static string Current
        {
            get => current;
            set
            {
                if (!IsSupported(value) || value == current)
                    return;

                current = value;
                LocSource.Instance.Refresh();   // every {local:Loc ...} in open windows updates
                LanguageChanged?.Invoke();      // code-built UI (tray menu) rebuilds itself
            }
        }

        public static bool IsSupported(string code)
        {
            foreach (UiLanguage language in Languages)
                if (language.Code == code) return true;
            return false;
        }

        private static string DetectDefault()
        {
            string code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return IsSupported(code) ? code : "en";
        }

        public static string T(string key, params object[] args)
        {
            string text = Lookup(current, key) ?? Lookup("en", key) ?? key;
            return args != null && args.Length > 0 ? string.Format(text, args) : text;
        }

        private static string Lookup(string language, string key)
        {
            return Strings.TryGetValue(language, out var table) && table.TryGetValue(key, out var value) ? value : null;
        }

        private static readonly Dictionary<string, Dictionary<string, string>> Strings =
            new Dictionary<string, Dictionary<string, string>>
            {
                // ------------------------------------------------------------ English
                ["en"] = new Dictionary<string, string>
                {
                    ["lang.en"] = "English",
                    ["lang.tr"] = "Turkish",
                    ["lang.ru"] = "Russian",
                    ["lang.es"] = "Spanish",
                    ["lang.fr"] = "French",
                    ["lang.it"] = "Italian",
                    ["lang.de"] = "German",

                    ["settings.window"] = "ScreenTranslator settings",
                    ["settings.title"] = "Settings",
                    ["settings.subtitle"] = "Choose how ScreenTranslator reads and translates the text you select.",
                    ["section.language"] = "Language",
                    ["ui.language.label"] = "Interface language",
                    ["ui.language.hint"] = "Applied instantly to menus, settings, history and messages.",
                    ["section.translation"] = "Translation",
                    ["swap"] = "Swap languages",
                    ["engine.label"] = "Translation engine",
                    ["engine.hint"] = "If one engine can't be reached, the other is tried automatically.",
                    ["from"] = "Translate from",
                    ["to"] = "Translate to",
                    ["auto.label"] = "Detect the language automatically",
                    ["auto.hint"] = "Use this when the text on screen can be in different languages.",
                    ["section.shortcut"] = "Shortcut",
                    ["shortcut.label"] = "Capture shortcut",
                    ["shortcut.reset"] = "Reset to default",
                    ["section.general"] = "General",
                    ["startup.label"] = "Start with Windows",
                    ["startup.hint"] = "ScreenTranslator waits in the tray until you press the shortcut.",
                    ["btn.cancel"] = "Cancel",
                    ["btn.save"] = "Save changes",
                    ["hotkey.hint.default"] = "Click the box, then press the keys you want to use.",
                    ["hotkey.hint.recording"] = "Press the new shortcut, or Esc to cancel.",
                    ["hotkey.hint.modifier"] = "Include Ctrl, Alt, Shift or Win so the shortcut doesn't fire while you type.",
                    ["hotkey.hint.recorded"] = "Shortcut recorded. Save to apply it.",
                    ["note.auto"] = "Auto-detect reads with every installed language pack, so it can be a little slower.",
                    ["note.nodata"] = "No text recognition data found. Add .traineddata files to the tessdata folder next to the app.",
                    ["note.missing"] = "Text recognition data for {0} is missing. Add {1}.traineddata to the tessdata folder next to the app.",
                    ["err.samelang"] = "The source and target language are the same. Pick two different languages.",
                    ["err.hotkeyused"] = "Another app already uses {0}. Try a different shortcut.",
                    ["err.save"] = "Couldn't save settings: {0}",

                    ["history.window"] = "Translation history",
                    ["history.title"] = "History",
                    ["history.subtitle"] = "Your last 20 translations from this session.",
                    ["history.copy"] = "Copy",
                    ["history.empty"] = "Nothing here yet. Translations you make will show up in this list.",
                    ["history.clear"] = "Clear history",

                    ["tray.translate"] = "Translate screen area",
                    ["tray.history"] = "History",
                    ["tray.settings"] = "Settings",
                    ["tray.exit"] = "Exit",
                    ["overlay.hint"] = "Drag over the text you want to translate. Press Esc to cancel.",
                    ["popup.translating"] = "Translating…",
                    ["popup.copy"] = "Copy translation",
                    ["popup.close"] = "Close",
                    ["balloon.hotkeyused"] = "The shortcut {0} is already used by another app. Pick a different one in Settings.",

                    ["err.generic"] = "Something went wrong while translating. Please try again.",
                    ["err.capture"] = "Couldn't capture the screen. Please try again.",
                    ["err.nodata"] = "Text recognition data is missing. Copy {0}.traineddata into the tessdata folder next to the app.",
                    ["err.ocr"] = "Text recognition failed ({0}). Check that the Tesseract files are installed correctly.",
                    ["err.notext"] = "No readable text found. Try selecting a tighter area around the text.",
                    ["err.network"] = "Couldn't reach the translation service. Check your internet connection and try again.",
                },

                // ------------------------------------------------------------ Türkçe
                ["tr"] = new Dictionary<string, string>
                {
                    ["lang.en"] = "İngilizce",
                    ["lang.tr"] = "Türkçe",
                    ["lang.ru"] = "Rusça",
                    ["lang.es"] = "İspanyolca",
                    ["lang.fr"] = "Fransızca",
                    ["lang.it"] = "İtalyanca",
                    ["lang.de"] = "Almanca",

                    ["settings.window"] = "ScreenTranslator ayarları",
                    ["settings.title"] = "Ayarlar",
                    ["settings.subtitle"] = "ScreenTranslator'ın seçtiğin metni nasıl okuyup çevireceğini belirle.",
                    ["section.language"] = "Dil",
                    ["ui.language.label"] = "Arayüz dili",
                    ["ui.language.hint"] = "Menülere, ayarlara, geçmişe ve mesajlara anında uygulanır.",
                    ["section.translation"] = "Çeviri",
                    ["swap"] = "Dilleri değiştir",
                    ["engine.label"] = "Çeviri motoru",
                    ["engine.hint"] = "Bir motora ulaşılamazsa diğeri otomatik denenir.",
                    ["from"] = "Kaynak dil",
                    ["to"] = "Hedef dil",
                    ["auto.label"] = "Dili otomatik algıla",
                    ["auto.hint"] = "Ekrandaki metin farklı dillerde olabiliyorsa kullan.",
                    ["section.shortcut"] = "Kısayol",
                    ["shortcut.label"] = "Yakalama kısayolu",
                    ["shortcut.reset"] = "Varsayılana dön",
                    ["section.general"] = "Genel",
                    ["startup.label"] = "Windows ile başlat",
                    ["startup.hint"] = "ScreenTranslator, kısayola basana kadar sistem tepsisinde bekler.",
                    ["btn.cancel"] = "İptal",
                    ["btn.save"] = "Değişiklikleri kaydet",
                    ["hotkey.hint.default"] = "Kutuya tıkla, sonra kullanmak istediğin tuşlara bas.",
                    ["hotkey.hint.recording"] = "Yeni kısayola bas veya iptal için Esc'e bas.",
                    ["hotkey.hint.modifier"] = "Yazı yazarken tetiklenmemesi için Ctrl, Alt, Shift veya Win ekle.",
                    ["hotkey.hint.recorded"] = "Kısayol kaydedildi. Uygulamak için Kaydet'e bas.",
                    ["note.auto"] = "Otomatik algılama kurulu tüm dil paketleriyle okur, bu yüzden biraz daha yavaş olabilir.",
                    ["note.nodata"] = "Metin tanıma verisi bulunamadı. Uygulamanın yanındaki tessdata klasörüne .traineddata dosyaları ekle.",
                    ["note.missing"] = "{0} için metin tanıma verisi eksik. Uygulamanın yanındaki tessdata klasörüne {1}.traineddata ekle.",
                    ["err.samelang"] = "Kaynak ve hedef dil aynı. İki farklı dil seç.",
                    ["err.hotkeyused"] = "{0} kısayolunu başka bir uygulama kullanıyor. Farklı bir kısayol dene.",
                    ["err.save"] = "Ayarlar kaydedilemedi: {0}",

                    ["history.window"] = "Çeviri geçmişi",
                    ["history.title"] = "Geçmiş",
                    ["history.subtitle"] = "Bu oturumdaki son 20 çevirin.",
                    ["history.copy"] = "Kopyala",
                    ["history.empty"] = "Henüz bir şey yok. Yaptığın çeviriler bu listede görünecek.",
                    ["history.clear"] = "Geçmişi temizle",

                    ["tray.translate"] = "Ekran alanını çevir",
                    ["tray.history"] = "Geçmiş",
                    ["tray.settings"] = "Ayarlar",
                    ["tray.exit"] = "Çıkış",
                    ["overlay.hint"] = "Çevirmek istediğin metnin üzerinde sürükle. İptal için Esc'e bas.",
                    ["popup.translating"] = "Çevriliyor…",
                    ["popup.copy"] = "Çeviriyi kopyala",
                    ["popup.close"] = "Kapat",
                    ["balloon.hotkeyused"] = "{0} kısayolu başka bir uygulama tarafından kullanılıyor. Ayarlar'dan farklı bir tane seç.",

                    ["err.generic"] = "Çeviri sırasında bir sorun oluştu. Lütfen tekrar dene.",
                    ["err.capture"] = "Ekran görüntüsü alınamadı. Lütfen tekrar dene.",
                    ["err.nodata"] = "Metin tanıma verisi eksik. {0}.traineddata dosyasını uygulamanın yanındaki tessdata klasörüne kopyala.",
                    ["err.ocr"] = "Metin tanıma başarısız oldu ({0}). Tesseract dosyalarının doğru kurulduğundan emin ol.",
                    ["err.notext"] = "Okunabilir metin bulunamadı. Metnin etrafında daha dar bir alan seçmeyi dene.",
                    ["err.network"] = "Çeviri servisine ulaşılamadı. İnternet bağlantını kontrol edip tekrar dene.",
                },

                // ------------------------------------------------------------ Русский
                ["ru"] = new Dictionary<string, string>
                {
                    ["lang.en"] = "Английский",
                    ["lang.tr"] = "Турецкий",
                    ["lang.ru"] = "Русский",
                    ["lang.es"] = "Испанский",
                    ["lang.fr"] = "Французский",
                    ["lang.it"] = "Итальянский",
                    ["lang.de"] = "Немецкий",

                    ["settings.window"] = "Настройки ScreenTranslator",
                    ["settings.title"] = "Настройки",
                    ["settings.subtitle"] = "Укажите, как ScreenTranslator распознаёт и переводит выделенный текст.",
                    ["section.language"] = "Язык",
                    ["ui.language.label"] = "Язык интерфейса",
                    ["ui.language.hint"] = "Сразу применяется к меню, настройкам, истории и сообщениям.",
                    ["section.translation"] = "Перевод",
                    ["swap"] = "Поменять языки",
                    ["engine.label"] = "Сервис перевода",
                    ["engine.hint"] = "Если один сервис недоступен, автоматически используется другой.",
                    ["from"] = "Перевести с",
                    ["to"] = "Перевести на",
                    ["auto.label"] = "Определять язык автоматически",
                    ["auto.hint"] = "Включите, если текст на экране может быть на разных языках.",
                    ["section.shortcut"] = "Сочетание клавиш",
                    ["shortcut.label"] = "Сочетание для захвата",
                    ["shortcut.reset"] = "Сбросить по умолчанию",
                    ["section.general"] = "Общие",
                    ["startup.label"] = "Запускать вместе с Windows",
                    ["startup.hint"] = "ScreenTranslator ждёт в трее, пока вы не нажмёте сочетание клавиш.",
                    ["btn.cancel"] = "Отмена",
                    ["btn.save"] = "Сохранить изменения",
                    ["hotkey.hint.default"] = "Щёлкните по полю и нажмите нужные клавиши.",
                    ["hotkey.hint.recording"] = "Нажмите новое сочетание или Esc для отмены.",
                    ["hotkey.hint.modifier"] = "Добавьте Ctrl, Alt, Shift или Win, чтобы сочетание не срабатывало при наборе текста.",
                    ["hotkey.hint.recorded"] = "Сочетание записано. Нажмите «Сохранить», чтобы применить.",
                    ["note.auto"] = "Автоопределение использует все установленные языковые пакеты, поэтому может работать немного медленнее.",
                    ["note.nodata"] = "Данные для распознавания текста не найдены. Добавьте файлы .traineddata в папку tessdata рядом с приложением.",
                    ["note.missing"] = "Нет данных для распознавания языка «{0}». Добавьте {1}.traineddata в папку tessdata рядом с приложением.",
                    ["err.samelang"] = "Исходный и целевой языки совпадают. Выберите два разных языка.",
                    ["err.hotkeyused"] = "Сочетание {0} уже используется другим приложением. Выберите другое.",
                    ["err.save"] = "Не удалось сохранить настройки: {0}",

                    ["history.window"] = "История переводов",
                    ["history.title"] = "История",
                    ["history.subtitle"] = "Последние 20 переводов за этот сеанс.",
                    ["history.copy"] = "Копировать",
                    ["history.empty"] = "Пока ничего нет. Ваши переводы появятся в этом списке.",
                    ["history.clear"] = "Очистить историю",

                    ["tray.translate"] = "Перевести область экрана",
                    ["tray.history"] = "История",
                    ["tray.settings"] = "Настройки",
                    ["tray.exit"] = "Выход",
                    ["overlay.hint"] = "Выделите мышью текст для перевода. Esc — отмена.",
                    ["popup.translating"] = "Перевод…",
                    ["popup.copy"] = "Копировать перевод",
                    ["popup.close"] = "Закрыть",
                    ["balloon.hotkeyused"] = "Сочетание {0} уже занято другим приложением. Выберите другое в настройках.",

                    ["err.generic"] = "Во время перевода что-то пошло не так. Попробуйте ещё раз.",
                    ["err.capture"] = "Не удалось сделать снимок экрана. Попробуйте ещё раз.",
                    ["err.nodata"] = "Нет данных для распознавания текста. Скопируйте {0}.traineddata в папку tessdata рядом с приложением.",
                    ["err.ocr"] = "Не удалось распознать текст ({0}). Проверьте, правильно ли установлены файлы Tesseract.",
                    ["err.notext"] = "Читаемый текст не найден. Попробуйте выделить область поуже вокруг текста.",
                    ["err.network"] = "Не удалось подключиться к сервису перевода. Проверьте подключение к интернету и повторите попытку.",
                },

                // ------------------------------------------------------------ Español
                ["es"] = new Dictionary<string, string>
                {
                    ["lang.en"] = "Inglés",
                    ["lang.tr"] = "Turco",
                    ["lang.ru"] = "Ruso",
                    ["lang.es"] = "Español",
                    ["lang.fr"] = "Francés",
                    ["lang.it"] = "Italiano",
                    ["lang.de"] = "Alemán",

                    ["settings.window"] = "Configuración de ScreenTranslator",
                    ["settings.title"] = "Configuración",
                    ["settings.subtitle"] = "Elige cómo ScreenTranslator lee y traduce el texto que seleccionas.",
                    ["section.language"] = "Idioma",
                    ["ui.language.label"] = "Idioma de la interfaz",
                    ["ui.language.hint"] = "Se aplica al instante a menús, configuración, historial y mensajes.",
                    ["section.translation"] = "Traducción",
                    ["swap"] = "Intercambiar idiomas",
                    ["engine.label"] = "Motor de traducción",
                    ["engine.hint"] = "Si no se puede acceder a un motor, se prueba el otro automáticamente.",
                    ["from"] = "Traducir de",
                    ["to"] = "Traducir a",
                    ["auto.label"] = "Detectar el idioma automáticamente",
                    ["auto.hint"] = "Úsalo cuando el texto en pantalla pueda estar en distintos idiomas.",
                    ["section.shortcut"] = "Atajo",
                    ["shortcut.label"] = "Atajo de captura",
                    ["shortcut.reset"] = "Restablecer",
                    ["section.general"] = "General",
                    ["startup.label"] = "Iniciar con Windows",
                    ["startup.hint"] = "ScreenTranslator espera en la bandeja hasta que pulses el atajo.",
                    ["btn.cancel"] = "Cancelar",
                    ["btn.save"] = "Guardar cambios",
                    ["hotkey.hint.default"] = "Haz clic en el cuadro y pulsa las teclas que quieras usar.",
                    ["hotkey.hint.recording"] = "Pulsa el nuevo atajo o Esc para cancelar.",
                    ["hotkey.hint.modifier"] = "Incluye Ctrl, Alt, Mayús o Win para que el atajo no se active mientras escribes.",
                    ["hotkey.hint.recorded"] = "Atajo registrado. Guarda para aplicarlo.",
                    ["note.auto"] = "La detección automática lee con todos los paquetes de idioma instalados, por lo que puede ser algo más lenta.",
                    ["note.nodata"] = "No se encontraron datos de reconocimiento de texto. Añade archivos .traineddata a la carpeta tessdata junto a la aplicación.",
                    ["note.missing"] = "Faltan los datos de reconocimiento de texto para {0}. Añade {1}.traineddata a la carpeta tessdata junto a la aplicación.",
                    ["err.samelang"] = "El idioma de origen y el de destino son el mismo. Elige dos idiomas distintos.",
                    ["err.hotkeyused"] = "Otra aplicación ya usa {0}. Prueba con otro atajo.",
                    ["err.save"] = "No se pudo guardar la configuración: {0}",

                    ["history.window"] = "Historial de traducciones",
                    ["history.title"] = "Historial",
                    ["history.subtitle"] = "Tus últimas 20 traducciones de esta sesión.",
                    ["history.copy"] = "Copiar",
                    ["history.empty"] = "Todavía no hay nada. Tus traducciones aparecerán en esta lista.",
                    ["history.clear"] = "Borrar historial",

                    ["tray.translate"] = "Traducir área de pantalla",
                    ["tray.history"] = "Historial",
                    ["tray.settings"] = "Configuración",
                    ["tray.exit"] = "Salir",
                    ["overlay.hint"] = "Arrastra sobre el texto que quieres traducir. Pulsa Esc para cancelar.",
                    ["popup.translating"] = "Traduciendo…",
                    ["popup.copy"] = "Copiar traducción",
                    ["popup.close"] = "Cerrar",
                    ["balloon.hotkeyused"] = "El atajo {0} ya lo usa otra aplicación. Elige otro en Configuración.",

                    ["err.generic"] = "Algo salió mal al traducir. Inténtalo de nuevo.",
                    ["err.capture"] = "No se pudo capturar la pantalla. Inténtalo de nuevo.",
                    ["err.nodata"] = "Faltan los datos de reconocimiento de texto. Copia {0}.traineddata en la carpeta tessdata junto a la aplicación.",
                    ["err.ocr"] = "Falló el reconocimiento de texto ({0}). Comprueba que los archivos de Tesseract estén bien instalados.",
                    ["err.notext"] = "No se encontró texto legible. Prueba a seleccionar un área más ajustada alrededor del texto.",
                    ["err.network"] = "No se pudo conectar con el servicio de traducción. Comprueba tu conexión a internet e inténtalo de nuevo.",
                },

                // ------------------------------------------------------------ Français
                ["fr"] = new Dictionary<string, string>
                {
                    ["lang.en"] = "Anglais",
                    ["lang.tr"] = "Turc",
                    ["lang.ru"] = "Russe",
                    ["lang.es"] = "Espagnol",
                    ["lang.fr"] = "Français",
                    ["lang.it"] = "Italien",
                    ["lang.de"] = "Allemand",

                    ["settings.window"] = "Paramètres de ScreenTranslator",
                    ["settings.title"] = "Paramètres",
                    ["settings.subtitle"] = "Choisissez comment ScreenTranslator lit et traduit le texte que vous sélectionnez.",
                    ["section.language"] = "Langue",
                    ["ui.language.label"] = "Langue de l'interface",
                    ["ui.language.hint"] = "S'applique immédiatement aux menus, paramètres, historique et messages.",
                    ["section.translation"] = "Traduction",
                    ["swap"] = "Inverser les langues",
                    ["engine.label"] = "Moteur de traduction",
                    ["engine.hint"] = "Si un moteur est inaccessible, l'autre est essayé automatiquement.",
                    ["from"] = "Traduire depuis",
                    ["to"] = "Traduire vers",
                    ["auto.label"] = "Détecter la langue automatiquement",
                    ["auto.hint"] = "À utiliser quand le texte à l'écran peut être dans différentes langues.",
                    ["section.shortcut"] = "Raccourci",
                    ["shortcut.label"] = "Raccourci de capture",
                    ["shortcut.reset"] = "Rétablir par défaut",
                    ["section.general"] = "Général",
                    ["startup.label"] = "Démarrer avec Windows",
                    ["startup.hint"] = "ScreenTranslator reste dans la zone de notification jusqu'à ce que vous utilisiez le raccourci.",
                    ["btn.cancel"] = "Annuler",
                    ["btn.save"] = "Enregistrer les modifications",
                    ["hotkey.hint.default"] = "Cliquez dans la zone, puis appuyez sur les touches souhaitées.",
                    ["hotkey.hint.recording"] = "Appuyez sur le nouveau raccourci, ou sur Échap pour annuler.",
                    ["hotkey.hint.modifier"] = "Ajoutez Ctrl, Alt, Maj ou Win pour que le raccourci ne se déclenche pas pendant la saisie.",
                    ["hotkey.hint.recorded"] = "Raccourci enregistré. Enregistrez pour l'appliquer.",
                    ["note.auto"] = "La détection automatique lit avec tous les packs de langue installés ; elle peut donc être un peu plus lente.",
                    ["note.nodata"] = "Aucune donnée de reconnaissance de texte trouvée. Ajoutez des fichiers .traineddata dans le dossier tessdata à côté de l'application.",
                    ["note.missing"] = "Les données de reconnaissance de texte pour {0} sont manquantes. Ajoutez {1}.traineddata dans le dossier tessdata à côté de l'application.",
                    ["err.samelang"] = "La langue source et la langue cible sont identiques. Choisissez deux langues différentes.",
                    ["err.hotkeyused"] = "Une autre application utilise déjà {0}. Essayez un autre raccourci.",
                    ["err.save"] = "Impossible d'enregistrer les paramètres : {0}",

                    ["history.window"] = "Historique des traductions",
                    ["history.title"] = "Historique",
                    ["history.subtitle"] = "Vos 20 dernières traductions de cette session.",
                    ["history.copy"] = "Copier",
                    ["history.empty"] = "Rien pour l'instant. Vos traductions apparaîtront dans cette liste.",
                    ["history.clear"] = "Effacer l'historique",

                    ["tray.translate"] = "Traduire une zone de l'écran",
                    ["tray.history"] = "Historique",
                    ["tray.settings"] = "Paramètres",
                    ["tray.exit"] = "Quitter",
                    ["overlay.hint"] = "Faites glisser sur le texte à traduire. Appuyez sur Échap pour annuler.",
                    ["popup.translating"] = "Traduction…",
                    ["popup.copy"] = "Copier la traduction",
                    ["popup.close"] = "Fermer",
                    ["balloon.hotkeyused"] = "Le raccourci {0} est déjà utilisé par une autre application. Choisissez-en un autre dans les paramètres.",

                    ["err.generic"] = "Un problème est survenu pendant la traduction. Veuillez réessayer.",
                    ["err.capture"] = "Impossible de capturer l'écran. Veuillez réessayer.",
                    ["err.nodata"] = "Données de reconnaissance de texte manquantes. Copiez {0}.traineddata dans le dossier tessdata à côté de l'application.",
                    ["err.ocr"] = "Échec de la reconnaissance de texte ({0}). Vérifiez que les fichiers Tesseract sont correctement installés.",
                    ["err.notext"] = "Aucun texte lisible trouvé. Essayez de sélectionner une zone plus serrée autour du texte.",
                    ["err.network"] = "Impossible de joindre le service de traduction. Vérifiez votre connexion Internet et réessayez.",
                },

                // ------------------------------------------------------------ Italiano
                ["it"] = new Dictionary<string, string>
                {
                    ["lang.en"] = "Inglese",
                    ["lang.tr"] = "Turco",
                    ["lang.ru"] = "Russo",
                    ["lang.es"] = "Spagnolo",
                    ["lang.fr"] = "Francese",
                    ["lang.it"] = "Italiano",
                    ["lang.de"] = "Tedesco",

                    ["settings.window"] = "Impostazioni di ScreenTranslator",
                    ["settings.title"] = "Impostazioni",
                    ["settings.subtitle"] = "Scegli come ScreenTranslator legge e traduce il testo che selezioni.",
                    ["section.language"] = "Lingua",
                    ["ui.language.label"] = "Lingua dell'interfaccia",
                    ["ui.language.hint"] = "Si applica subito a menu, impostazioni, cronologia e messaggi.",
                    ["section.translation"] = "Traduzione",
                    ["swap"] = "Scambia le lingue",
                    ["engine.label"] = "Motore di traduzione",
                    ["engine.hint"] = "Se un motore non è raggiungibile, viene provato automaticamente l'altro.",
                    ["from"] = "Traduci da",
                    ["to"] = "Traduci in",
                    ["auto.label"] = "Rileva la lingua automaticamente",
                    ["auto.hint"] = "Usalo quando il testo sullo schermo può essere in lingue diverse.",
                    ["section.shortcut"] = "Scorciatoia",
                    ["shortcut.label"] = "Scorciatoia di cattura",
                    ["shortcut.reset"] = "Ripristina predefinita",
                    ["section.general"] = "Generale",
                    ["startup.label"] = "Avvia con Windows",
                    ["startup.hint"] = "ScreenTranslator resta nell'area di notifica finché non premi la scorciatoia.",
                    ["btn.cancel"] = "Annulla",
                    ["btn.save"] = "Salva modifiche",
                    ["hotkey.hint.default"] = "Fai clic sulla casella, poi premi i tasti che vuoi usare.",
                    ["hotkey.hint.recording"] = "Premi la nuova scorciatoia, oppure Esc per annullare.",
                    ["hotkey.hint.modifier"] = "Includi Ctrl, Alt, Maiusc o Win perché la scorciatoia non scatti mentre scrivi.",
                    ["hotkey.hint.recorded"] = "Scorciatoia registrata. Salva per applicarla.",
                    ["note.auto"] = "Il rilevamento automatico legge con tutti i pacchetti lingua installati, quindi può essere un po' più lento.",
                    ["note.nodata"] = "Nessun dato di riconoscimento del testo trovato. Aggiungi file .traineddata nella cartella tessdata accanto all'app.",
                    ["note.missing"] = "Mancano i dati di riconoscimento del testo per {0}. Aggiungi {1}.traineddata nella cartella tessdata accanto all'app.",
                    ["err.samelang"] = "La lingua di origine e quella di destinazione sono uguali. Scegli due lingue diverse.",
                    ["err.hotkeyused"] = "Un'altra app usa già {0}. Prova un'altra scorciatoia.",
                    ["err.save"] = "Impossibile salvare le impostazioni: {0}",

                    ["history.window"] = "Cronologia traduzioni",
                    ["history.title"] = "Cronologia",
                    ["history.subtitle"] = "Le tue ultime 20 traduzioni di questa sessione.",
                    ["history.copy"] = "Copia",
                    ["history.empty"] = "Ancora niente. Le traduzioni che farai appariranno in questo elenco.",
                    ["history.clear"] = "Cancella cronologia",

                    ["tray.translate"] = "Traduci area dello schermo",
                    ["tray.history"] = "Cronologia",
                    ["tray.settings"] = "Impostazioni",
                    ["tray.exit"] = "Esci",
                    ["overlay.hint"] = "Trascina sul testo da tradurre. Premi Esc per annullare.",
                    ["popup.translating"] = "Traduzione in corso…",
                    ["popup.copy"] = "Copia traduzione",
                    ["popup.close"] = "Chiudi",
                    ["balloon.hotkeyused"] = "La scorciatoia {0} è già usata da un'altra app. Scegline un'altra nelle impostazioni.",

                    ["err.generic"] = "Qualcosa è andato storto durante la traduzione. Riprova.",
                    ["err.capture"] = "Impossibile catturare lo schermo. Riprova.",
                    ["err.nodata"] = "Dati di riconoscimento del testo mancanti. Copia {0}.traineddata nella cartella tessdata accanto all'app.",
                    ["err.ocr"] = "Riconoscimento del testo non riuscito ({0}). Controlla che i file di Tesseract siano installati correttamente.",
                    ["err.notext"] = "Nessun testo leggibile trovato. Prova a selezionare un'area più stretta attorno al testo.",
                    ["err.network"] = "Impossibile raggiungere il servizio di traduzione. Controlla la connessione a Internet e riprova.",
                },

                // ------------------------------------------------------------ Deutsch
                ["de"] = new Dictionary<string, string>
                {
                    ["lang.en"] = "Englisch",
                    ["lang.tr"] = "Türkisch",
                    ["lang.ru"] = "Russisch",
                    ["lang.es"] = "Spanisch",
                    ["lang.fr"] = "Französisch",
                    ["lang.it"] = "Italienisch",
                    ["lang.de"] = "Deutsch",

                    ["settings.window"] = "ScreenTranslator-Einstellungen",
                    ["settings.title"] = "Einstellungen",
                    ["settings.subtitle"] = "Lege fest, wie ScreenTranslator den ausgewählten Text liest und übersetzt.",
                    ["section.language"] = "Sprache",
                    ["ui.language.label"] = "Oberflächensprache",
                    ["ui.language.hint"] = "Wird sofort auf Menüs, Einstellungen, Verlauf und Meldungen angewendet.",
                    ["section.translation"] = "Übersetzung",
                    ["swap"] = "Sprachen tauschen",
                    ["engine.label"] = "Übersetzungsdienst",
                    ["engine.hint"] = "Ist ein Dienst nicht erreichbar, wird automatisch der andere versucht.",
                    ["from"] = "Übersetzen von",
                    ["to"] = "Übersetzen nach",
                    ["auto.label"] = "Sprache automatisch erkennen",
                    ["auto.hint"] = "Nutze das, wenn der Text auf dem Bildschirm in verschiedenen Sprachen sein kann.",
                    ["section.shortcut"] = "Tastenkombination",
                    ["shortcut.label"] = "Aufnahme-Tastenkombination",
                    ["shortcut.reset"] = "Auf Standard zurücksetzen",
                    ["section.general"] = "Allgemein",
                    ["startup.label"] = "Mit Windows starten",
                    ["startup.hint"] = "ScreenTranslator wartet im Infobereich, bis du die Tastenkombination drückst.",
                    ["btn.cancel"] = "Abbrechen",
                    ["btn.save"] = "Änderungen speichern",
                    ["hotkey.hint.default"] = "Klicke in das Feld und drücke die gewünschten Tasten.",
                    ["hotkey.hint.recording"] = "Drücke die neue Tastenkombination oder Esc zum Abbrechen.",
                    ["hotkey.hint.modifier"] = "Füge Strg, Alt, Umschalt oder Win hinzu, damit sie beim Tippen nicht auslöst.",
                    ["hotkey.hint.recorded"] = "Tastenkombination aufgenommen. Speichere, um sie zu übernehmen.",
                    ["note.auto"] = "Die automatische Erkennung liest mit allen installierten Sprachpaketen und kann daher etwas langsamer sein.",
                    ["note.nodata"] = "Keine Texterkennungsdaten gefunden. Füge .traineddata-Dateien zum Ordner tessdata neben der App hinzu.",
                    ["note.missing"] = "Texterkennungsdaten für {0} fehlen. Füge {1}.traineddata zum Ordner tessdata neben der App hinzu.",
                    ["err.samelang"] = "Quell- und Zielsprache sind identisch. Wähle zwei verschiedene Sprachen.",
                    ["err.hotkeyused"] = "{0} wird bereits von einer anderen App verwendet. Probiere eine andere Kombination.",
                    ["err.save"] = "Einstellungen konnten nicht gespeichert werden: {0}",

                    ["history.window"] = "Übersetzungsverlauf",
                    ["history.title"] = "Verlauf",
                    ["history.subtitle"] = "Deine letzten 20 Übersetzungen in dieser Sitzung.",
                    ["history.copy"] = "Kopieren",
                    ["history.empty"] = "Noch nichts vorhanden. Deine Übersetzungen erscheinen in dieser Liste.",
                    ["history.clear"] = "Verlauf löschen",

                    ["tray.translate"] = "Bildschirmbereich übersetzen",
                    ["tray.history"] = "Verlauf",
                    ["tray.settings"] = "Einstellungen",
                    ["tray.exit"] = "Beenden",
                    ["overlay.hint"] = "Ziehe über den Text, den du übersetzen möchtest. Esc bricht ab.",
                    ["popup.translating"] = "Wird übersetzt…",
                    ["popup.copy"] = "Übersetzung kopieren",
                    ["popup.close"] = "Schließen",
                    ["balloon.hotkeyused"] = "Die Tastenkombination {0} wird bereits von einer anderen App verwendet. Wähle in den Einstellungen eine andere.",

                    ["err.generic"] = "Beim Übersetzen ist etwas schiefgelaufen. Bitte versuche es erneut.",
                    ["err.capture"] = "Der Bildschirm konnte nicht erfasst werden. Bitte versuche es erneut.",
                    ["err.nodata"] = "Texterkennungsdaten fehlen. Kopiere {0}.traineddata in den Ordner tessdata neben der App.",
                    ["err.ocr"] = "Texterkennung fehlgeschlagen ({0}). Prüfe, ob die Tesseract-Dateien korrekt installiert sind.",
                    ["err.notext"] = "Kein lesbarer Text gefunden. Wähle einen engeren Bereich um den Text.",
                    ["err.network"] = "Der Übersetzungsdienst ist nicht erreichbar. Prüfe deine Internetverbindung und versuche es erneut.",
                },
            };
    }

    /// <summary>
    /// Lets XAML bind to Loc.T so texts refresh the moment the language changes.
    /// Bound as Loc["some.key"]; raising "Item[]" re-reads every one of them.
    /// </summary>
    public sealed class LocSource : INotifyPropertyChanged
    {
        public static LocSource Instance { get; } = new LocSource();

        public string this[string key] => Loc.T(key);

        public event PropertyChangedEventHandler PropertyChanged;

        internal void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    /// <summary>XAML usage: Text="{local:Loc settings.title}" (updates live).</summary>
    public sealed class LocExtension : MarkupExtension
    {
        public LocExtension(string key)
        {
            Key = key;
        }

        [ConstructorArgument("key")]
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // Fully qualified: System.Windows.Forms also has a Binding type.
            var binding = new System.Windows.Data.Binding("[" + Key + "]")
            {
                Source = LocSource.Instance,
                Mode = System.Windows.Data.BindingMode.OneWay
            };
            return binding.ProvideValue(serviceProvider);
        }
    }
}