# ScreenTranslator

A lightweight Windows desktop tool that lets you capture any part of your screen and get an instant translation — no copy-pasting, no switching windows. Draw a box around any text, and a translation pops up right where you need it.

Built for situations where text isn't selectable: games, embedded UI, images, videos, or anything rendered outside a normal text field.

## Features

- **One shortcut, anywhere** — trigger a screen capture with a global hotkey (default `Ctrl+Alt+X`, configurable) from any application.
- **Smart OCR** — powered by the Tesseract engine, with per-line confidence filtering and adaptive contrast fallback so it reads reliably even on dark themes, complex UIs, or busy backgrounds.
- **7 languages** — translate between English, Turkish, Russian, Spanish, French, Italian, and German, with optional auto-detection of the source language.
- **Choice of translation engine** — Google Translate for speed, or Yandex Translate for better handling of informal/game-context phrasing.
- **Translation history** — the last 20 translations are kept and browsable from the tray icon.
- **One-click copy** — copy any translation to your clipboard directly from the popup.
- **Runs quietly in the background** — lives in the system tray, stays out of your way until you need it.
- **Launch on startup** — optional, so it's ready the moment Windows boots.

## Tech Stack

- C# / WPF (.NET 8)
- Tesseract OCR
- GTranslate (Google & Yandex APIs)
- xUnit (unit tests for the OCR/translation pipeline)
- GitHub Actions (automated build, packaging, and releases)

## Getting Started

**Option A — Installer (recommended):**

1. Go to the **Releases** section on the right side of this page.
2. Download the latest `ScreenTranslator_Setup_X.X.exe`.
3. Run it and follow the setup wizard — it adds a Start Menu shortcut and an optional desktop shortcut, and can be removed cleanly later via "Uninstall ScreenTranslator".

**Option B — Portable (no install):**

1. Download the latest `ScreenTranslator_vX.X.zip` from Releases instead.
2. Extract the folder and run `ScreenTranslator.exe` directly.

Either way, on first launch the settings window opens automatically — pick your languages, translation engine, and shortcut key. Press your shortcut, drag a box around any text on your screen, and release — the translation appears right below your selection.

## Settings

Right-click the tray icon anytime to reopen Settings and change:
- Source/target language, or enable auto-detect for the source language
- Translation engine (Google / Yandex)
- Capture shortcut
- Start with Windows

Right-click the tray icon and choose **History** to browse your last 20 translations.

## Roadmap / Known Limitations

- Multi-monitor setups with mixed DPI scaling are supported but not extensively tested — feedback welcome.
- Very low-contrast text (e.g. light text on a busy, colorful background) may occasionally be missed.
- The free translation APIs (Google/Yandex) are rate-limited — very rapid, repeated captures may briefly fail with a "too many requests" error.

## Contributing

Issues and pull requests are welcome. If OCR misreads a specific kind of UI text, opening an issue with a screenshot helps a lot.
