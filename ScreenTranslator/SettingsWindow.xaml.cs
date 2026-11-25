#nullable disable
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Brush = System.Windows.Media.Brush;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ScreenTranslator
{
    public partial class SettingsWindow : Window
    {
        private uint pendingModifiers;
        private uint pendingKey;

        public SettingsWindow()
        {
            InitializeComponent();
            SourceInitialized += (s, e) => NativeMethods.UseDarkTitleBar(this);

            CmbUiLang.ItemsSource = Loc.Languages;
            CmbUiLang.SelectedValue = Loc.Current;
            if (CmbUiLang.SelectedIndex < 0) CmbUiLang.SelectedIndex = 0;

            CmbSourceLang.ItemsSource = AppLanguages.All;
            CmbTargetLang.ItemsSource = AppLanguages.All;

            CmbApi.SelectedIndex = AppSettings.SelectedApi == "Yandex" ? 1 : 0;
            CmbSourceLang.SelectedValue = AppSettings.SourceLang;
            CmbTargetLang.SelectedValue = AppSettings.TargetLang;
            if (CmbSourceLang.SelectedIndex < 0) CmbSourceLang.SelectedIndex = 0;
            if (CmbTargetLang.SelectedIndex < 0) CmbTargetLang.SelectedIndex = 1;

            ChkAutoDetect.IsChecked = AppSettings.AutoDetectSource;
            ChkStartUp.IsChecked = AppSettings.AutoStart;

            pendingModifiers = AppSettings.HotkeyModifiers;
            pendingKey = AppSettings.HotkeyKey;
            TxtHotkey.Text = AppSettings.FormatHotkey(pendingModifiers, pendingKey);
            TxtHotkeyHint.Text = Loc.T("hotkey.hint.default");

            UpdateLanguageState();

            // Wired after the initial values are set so they don't fire during setup.
            CmbSourceLang.SelectionChanged += (s, e) => UpdateLanguageState();
            ChkAutoDetect.Checked += (s, e) => UpdateLanguageState();
            ChkAutoDetect.Unchecked += (s, e) => UpdateLanguageState();
            CmbUiLang.SelectionChanged += CmbUiLang_SelectionChanged;
        }

        // ------------------------------------------------------------------
        // Interface language (applies instantly, no Save needed)
        // ------------------------------------------------------------------

        private void CmbUiLang_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string code = CmbUiLang.SelectedValue as string;
            if (!Loc.IsSupported(code) || code == Loc.Current)
                return;

            // Raises Loc.LanguageChanged: every {local:Loc ...} text in every open window and the tray menu update now.
            AppSettings.UiLanguage = code;

            // Remember the choice right away; the other pending changes still wait for "Save changes".
            try { AppSettings.Save(); } catch (Exception ex) { AppLog.Write("Could not save language: " + ex.Message); }

            RefreshLanguageLists();
            TxtHotkeyHint.Text = Loc.T(HotkeyBox.IsKeyboardFocused ? "hotkey.hint.recording" : "hotkey.hint.default");
            HideError();
        }

        /// <summary>Language names come from code, so the two lists are rebuilt in the new language.</summary>
        private void RefreshLanguageLists()
        {
            object source = CmbSourceLang.SelectedValue;
            object target = CmbTargetLang.SelectedValue;

            CmbSourceLang.ItemsSource = null;
            CmbSourceLang.ItemsSource = AppLanguages.All;
            CmbSourceLang.SelectedValue = source;

            CmbTargetLang.ItemsSource = null;
            CmbTargetLang.ItemsSource = AppLanguages.All;
            CmbTargetLang.SelectedValue = target;

            UpdateLanguageState();
        }

        // ------------------------------------------------------------------
        // Languages
        // ------------------------------------------------------------------

        private void UpdateLanguageState()
        {
            bool auto = ChkAutoDetect.IsChecked == true;
            CmbSourceLang.IsEnabled = !auto;
            BtnSwap.IsEnabled = !auto;

            if (auto)
            {
                bool anyInstalled = AppLanguages.All.Any(l => TranslationService.IsLanguageDataInstalled(l.Tess));
                if (anyInstalled)
                    ShowLanguageNote(Loc.T("note.auto"), false);
                else
                    ShowLanguageNote(Loc.T("note.nodata"), true);
                return;
            }

            var source = CmbSourceLang.SelectedItem as AppLanguage;
            if (source != null && !TranslationService.IsLanguageDataInstalled(source.Tess))
                ShowLanguageNote(Loc.T("note.missing", source.Name, source.Tess), true);
            else
                TxtLangNote.Visibility = Visibility.Collapsed;
        }

        private void ShowLanguageNote(string text, bool isWarning)
        {
            TxtLangNote.Text = text;
            TxtLangNote.Foreground = (Brush)FindResource(isWarning ? "Danger" : "TextMuted");
            TxtLangNote.Visibility = Visibility.Visible;
        }

        private void BtnSwap_Click(object sender, RoutedEventArgs e)
        {
            object source = CmbSourceLang.SelectedValue;
            object target = CmbTargetLang.SelectedValue;
            CmbSourceLang.SelectedValue = target;
            CmbTargetLang.SelectedValue = source;
        }

        // ------------------------------------------------------------------
        // Shortcut recorder
        // ------------------------------------------------------------------

        private void HotkeyBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            HotkeyBox.Focus();
            e.Handled = true;
        }

        private void HotkeyBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            HotkeyBox.BorderBrush = (Brush)FindResource("Accent");
            TxtHotkeyHint.Text = Loc.T("hotkey.hint.recording");
        }

        private void HotkeyBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            HotkeyBox.BorderBrush = (Brush)FindResource("LineBrush");
            TxtHotkeyHint.Text = Loc.T("hotkey.hint.default");
        }

        private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key key = e.Key;
            if (key == Key.System) key = e.SystemKey;
            else if (key == Key.ImeProcessed) key = e.ImeProcessedKey;
            else if (key == Key.DeadCharProcessed) key = e.DeadCharProcessedKey;

            ModifierKeys modifiers = Keyboard.Modifiers;

            if (key == Key.Tab && modifiers == ModifierKeys.None)
                return; // let keyboard navigation leave the box

            e.Handled = true;

            if (key == Key.Escape && modifiers == ModifierKeys.None)
            {
                HotkeyBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                return;
            }

            if (key == Key.None || IsModifierKey(key))
                return; // wait for the actual key

            uint nativeModifiers = ToNativeModifiers(modifiers);
            if (nativeModifiers == 0)
            {
                TxtHotkeyHint.Text = Loc.T("hotkey.hint.modifier");
                return;
            }

            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            if (vk == 0)
                return;

            pendingModifiers = nativeModifiers;
            pendingKey = vk;
            TxtHotkey.Text = AppSettings.FormatHotkey(pendingModifiers, pendingKey);
            TxtHotkeyHint.Text = Loc.T("hotkey.hint.recorded");
            HideError();
        }

        private void BtnResetHotkey_Click(object sender, RoutedEventArgs e)
        {
            pendingModifiers = AppSettings.DefaultHotkeyModifiers;
            pendingKey = AppSettings.DefaultHotkeyKey;
            TxtHotkey.Text = AppSettings.FormatHotkey(pendingModifiers, pendingKey);
            HideError();
        }

        private static bool IsModifierKey(Key key) =>
            key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin;

        private static uint ToNativeModifiers(ModifierKeys modifiers)
        {
            uint result = 0;
            if ((modifiers & ModifierKeys.Control) != 0) result |= NativeMethods.ModControl;
            if ((modifiers & ModifierKeys.Alt) != 0) result |= NativeMethods.ModAlt;
            if ((modifiers & ModifierKeys.Shift) != 0) result |= NativeMethods.ModShift;
            if ((modifiers & ModifierKeys.Windows) != 0) result |= NativeMethods.ModWin;
            return result;
        }

        // ------------------------------------------------------------------
        // Save
        // ------------------------------------------------------------------

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var source = CmbSourceLang.SelectedItem as AppLanguage ?? AppLanguages.All[0];
            var target = CmbTargetLang.SelectedItem as AppLanguage ?? AppLanguages.All[1];
            bool auto = ChkAutoDetect.IsChecked == true;

            if (!auto && source.Code == target.Code)
            {
                ShowError(Loc.T("err.samelang"));
                return;
            }

            bool hotkeyChanged = pendingModifiers != AppSettings.HotkeyModifiers || pendingKey != AppSettings.HotkeyKey;
            if (hotkeyChanged)
            {
                IntPtr handle = new WindowInteropHelper(this).EnsureHandle();
                if (!NativeMethods.IsHotkeyAvailable(handle, pendingModifiers, pendingKey))
                {
                    ShowError(Loc.T("err.hotkeyused", AppSettings.FormatHotkey(pendingModifiers, pendingKey)));
                    return;
                }
            }

            AppSettings.SelectedApi = CmbApi.SelectedIndex == 1 ? "Yandex" : "Google";
            AppSettings.SourceLang = source.Code;
            AppSettings.TargetLang = target.Code;
            AppSettings.AutoDetectSource = auto;
            AppSettings.AutoStart = ChkStartUp.IsChecked == true;
            AppSettings.HotkeyModifiers = pendingModifiers;
            AppSettings.HotkeyKey = pendingKey;

            try
            {
                AppSettings.Save();
            }
            catch (Exception ex)
            {
                ShowError(Loc.T("err.save", ex.Message));
                return;
            }

            DialogResult = true;
        }

        private void ShowError(string message)
        {
            TxtError.Text = message;
            TxtError.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            TxtError.Visibility = Visibility.Collapsed;
        }
    }
}