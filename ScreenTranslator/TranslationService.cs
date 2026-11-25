#nullable disable
using GTranslate.Translators;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;

namespace ScreenTranslator
{
    public enum TranslationStatus
    {
        Success,
        NoText,
        LanguageDataMissing,
        OcrFailed,
        TranslationFailed
    }

    public class TranslationResult
    {
        public string ExtractedText { get; set; } = "";
        public string TranslatedText { get; set; } = "";
        public TranslationStatus Status { get; set; } = TranslationStatus.NoText;

        /// <summary>User-facing explanation when Status is not Success.</summary>
        public string Message { get; set; } = "";

        public string EngineUsed { get; set; } = "";

        public bool HasContent => Status == TranslationStatus.Success && !string.IsNullOrWhiteSpace(TranslatedText);
    }

    public class TranslationService : IDisposable
    {
        /// <summary>Words Tesseract is less sure about than this are dropped as noise.</summary>
        public const float MinWordConfidence = 35f;

        private const int MaxTranslateLength = 4500;
        private const int ImagePadding = 24;

        private readonly SemaphoreSlim ocrLock = new SemaphoreSlim(1, 1);
        private readonly GoogleTranslator googleTranslator = new GoogleTranslator();
        private readonly YandexTranslator yandexTranslator = new YandexTranslator();

        private TesseractEngine ocrEngine;
        private string ocrEngineLang;

        public static string TessDataPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");

        public static bool IsLanguageDataInstalled(string tessLang) =>
            File.Exists(Path.Combine(TessDataPath, tessLang + ".traineddata"));

        // ------------------------------------------------------------------
        // Public entry point
        // ------------------------------------------------------------------

        public async Task<TranslationResult> TranslateCaptureAsync(
            Bitmap capturedImage, string sourceLang, string targetLang, string tessLang, string selectedApi, bool autoDetectSource = false)
        {
            var result = new TranslationResult();

            string ocrLanguages = ResolveOcrLanguages(tessLang, autoDetectSource, out string missingLang);
            if (ocrLanguages == null)
            {
                result.Status = TranslationStatus.LanguageDataMissing;
                AppLog.Write("Missing traineddata: " + missingLang);
                result.Message = Loc.T("err.nodata", missingLang);
                return result;
            }

            string extractedText;
            try
            {
                extractedText = await Task.Run(() => RecognizeText(capturedImage, ocrLanguages)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScreenTranslator] OCR failed: {ex}");
                AppLog.Write("OCR failed: " + ex);
                result.Status = TranslationStatus.OcrFailed;
                result.Message = Loc.T("err.ocr", ex.GetType().Name);
                return result;
            }

            result.ExtractedText = extractedText;

            if (!extractedText.Any(char.IsLetterOrDigit))
            {
                result.Status = TranslationStatus.NoText;
                result.Message = Loc.T("err.notext");
                return result;
            }

            string textToTranslate = TrimForTranslation(extractedText);
            string effectiveSourceLang = autoDetectSource ? null : sourceLang;

            try
            {
                var (translated, engine) = await TranslateWithFallbackAsync(textToTranslate, targetLang, effectiveSourceLang, selectedApi)
                    .ConfigureAwait(false);

                result.TranslatedText = translated;
                result.EngineUsed = engine;
                result.Status = TranslationStatus.Success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScreenTranslator] Translation failed: {ex}");
                AppLog.Write("Translation failed: " + ex);
                result.Status = TranslationStatus.TranslationFailed;
                result.Message = Loc.T("err.network");
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Translation
        // ------------------------------------------------------------------

        private async Task<(string Text, string Engine)> TranslateWithFallbackAsync(
            string text, string targetLang, string sourceLang, string preferredApi)
        {
            string[] order = preferredApi == "Yandex" ? new[] { "Yandex", "Google" } : new[] { "Google", "Yandex" };
            Exception lastError = null;

            foreach (string engine in order)
            {
                try
                {
                    string translated = await WithTimeout(TranslateOnceAsync(engine, text, targetLang, sourceLang), TimeSpan.FromSeconds(15))
                        .ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(translated))
                        return (translated, engine);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Debug.WriteLine($"[ScreenTranslator] {engine} failed: {ex.Message}");
                }
            }

            throw lastError ?? new InvalidOperationException("The translation service returned an empty result.");
        }

        private async Task<string> TranslateOnceAsync(string engine, string text, string targetLang, string sourceLang)
        {
            if (engine == "Yandex")
            {
                var translation = await yandexTranslator.TranslateAsync(text, targetLang, sourceLang).ConfigureAwait(false);
                return translation.Translation;
            }
            else
            {
                var translation = await googleTranslator.TranslateAsync(text, targetLang, sourceLang).ConfigureAwait(false);
                return translation.Translation;
            }
        }

        private static async Task<T> WithTimeout<T>(Task<T> task, TimeSpan timeout)
        {
            Task finished = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (finished != task)
                throw new TimeoutException("The translation service did not respond in time.");

            return await task.ConfigureAwait(false);
        }

        private static string TrimForTranslation(string text)
        {
            if (text.Length <= MaxTranslateLength)
                return text;

            int cut = text.LastIndexOfAny(new[] { '.', '!', '?', '\n' }, MaxTranslateLength - 1);
            return cut > MaxTranslateLength / 2 ? text.Substring(0, cut + 1) : text.Substring(0, MaxTranslateLength);
        }

        // ------------------------------------------------------------------
        // OCR orchestration
        // ------------------------------------------------------------------

        private sealed class OcrOutput
        {
            public string Text = "";
            public double MeanConfidence;
            public int CharCount;

            // Rewards confident results but also longer ones, so a pass that kept three words
            // doesn't beat a pass that read the whole paragraph.
            public double Score => CharCount == 0 ? 0 : MeanConfidence * Math.Sqrt(CharCount);
        }

        private sealed class GrayImage
        {
            public byte[] Pixels;
            public int Width;
            public int Height;
        }

        private static string ResolveOcrLanguages(string preferredTess, bool autoDetect, out string missing)
        {
            missing = null;

            if (!autoDetect)
            {
                if (!IsLanguageDataInstalled(preferredTess))
                {
                    missing = preferredTess;
                    return null;
                }
                return preferredTess;
            }

            // Auto-detect: the text could be in any language, so read with every installed language pack.
            var languages = new List<string>();
            if (IsLanguageDataInstalled(preferredTess))
                languages.Add(preferredTess);

            foreach (AppLanguage language in AppLanguages.All)
            {
                if (!languages.Contains(language.Tess) && IsLanguageDataInstalled(language.Tess))
                    languages.Add(language.Tess);
            }

            if (languages.Count == 0)
            {
                missing = preferredTess;
                return null;
            }

            return string.Join("+", languages);
        }

        private string RecognizeText(Bitmap source, string languages)
        {
            ocrLock.Wait();
            try
            {
                TesseractEngine engine = GetOcrEngine(languages);

                GrayImage gray = ToUpscaledGray(source);
                if (IsDarkBackground(gray.Pixels))
                    Invert(gray.Pixels); // always hand Tesseract dark text on a light background
                StretchContrast(gray.Pixels);

                OcrOutput best = null;

                // Pass 1: clean grayscale, automatic layout.
                best = Better(best, RunVariant(engine, gray.Pixels, gray.Width, gray.Height, PageSegMode.Auto));
                if (IsGoodEnough(best)) return best.Text;

                // Pass 2: locally thresholded (handles gradients, photos, uneven backgrounds).
                byte[] binary = AdaptiveThreshold(gray);
                best = Better(best, RunVariant(engine, binary, gray.Width, gray.Height, PageSegMode.SingleBlock));
                if (IsGoodEnough(best)) return best.Text;

                // Pass 3: opposite polarity, in case the background guess was wrong.
                byte[] inverted = (byte[])gray.Pixels.Clone();
                Invert(inverted);
                best = Better(best, RunVariant(engine, inverted, gray.Width, gray.Height, PageSegMode.Auto));
                if (IsGoodEnough(best)) return best.Text;

                // Pass 4: scattered text (buttons, labels) or a single tight line.
                PageSegMode lastMode = source.Height <= 80 ? PageSegMode.SingleLine : PageSegMode.SparseText;
                best = Better(best, RunVariant(engine, gray.Pixels, gray.Width, gray.Height, lastMode));

                if (best == null || IsLikelyNoise(best))
                    return "";

                return best.Text;
            }
            finally
            {
                ocrLock.Release();
            }
        }

        private static OcrOutput Better(OcrOutput current, OcrOutput candidate)
        {
            if (candidate == null || candidate.CharCount == 0) return current;
            if (current == null) return candidate;
            return candidate.Score > current.Score ? candidate : current;
        }

        private static bool IsGoodEnough(OcrOutput output) =>
            output != null && output.CharCount >= 2 && output.MeanConfidence >= 78;

        private static bool IsLikelyNoise(OcrOutput output) =>
            output.MeanConfidence < 50 || (output.CharCount < 4 && output.MeanConfidence < 70);

        private TesseractEngine GetOcrEngine(string languages)
        {
            if (ocrEngine == null || ocrEngineLang != languages)
            {
                ocrEngine?.Dispose();
                ocrEngine = null;

                var engine = new TesseractEngine(TessDataPath, languages, EngineMode.LstmOnly);

                // No character whitelist: with the LSTM engine it forces wrong guesses ("l" for "|", random
                // letters for symbols) instead of rejecting them, and it blocked every accented letter
                // outside Turkish.
                engine.SetVariable("user_defined_dpi", "300");
                engine.SetVariable("preserve_interword_spaces", "1");

                ocrEngine = engine;
                ocrEngineLang = languages;
            }
            return ocrEngine;
        }

        private static OcrOutput RunVariant(TesseractEngine engine, byte[] pixels, int width, int height, PageSegMode mode)
        {
            using (Bitmap bitmap = ToPaddedBitmap(pixels, width, height, ImagePadding))
            {
                return RunOcr(engine, bitmap, mode);
            }
        }

        private static OcrOutput RunOcr(TesseractEngine engine, Bitmap bitmap, PageSegMode mode)
        {
            byte[] imageBytes;
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                imageBytes = stream.ToArray();
            }

            using (var img = Pix.LoadFromMemory(imageBytes))
            using (var page = engine.Process(img, mode))
            {
                if (string.IsNullOrWhiteSpace(page.GetText()))
                    return new OcrOutput();

                using (var iter = page.GetIterator())
                {
                    if (iter == null)
                        return new OcrOutput();

                    var lines = new List<(string Text, Tesseract.Rect Bounds)>();
                    var lineWords = new List<string>();
                    double confidenceSum = 0;
                    int confidenceChars = 0;

                    iter.Begin();
                    do
                    {
                        string word = iter.GetText(PageIteratorLevel.Word)?.Trim();
                        float confidence = iter.GetConfidence(PageIteratorLevel.Word);

                        if (IsUsableWord(word, confidence))
                        {
                            lineWords.Add(word);
                            confidenceSum += confidence * word.Length;
                            confidenceChars += word.Length;
                        }

                        if (iter.IsAtFinalOf(PageIteratorLevel.TextLine, PageIteratorLevel.Word))
                        {
                            if (lineWords.Count > 0)
                            {
                                string line = CleanLine(string.Join(" ", lineWords));
                                if (line.Length > 0 && IsReadableLine(line))
                                {
                                    if (!iter.TryGetBoundingBox(PageIteratorLevel.TextLine, out Tesseract.Rect bounds))
                                        bounds = default(Tesseract.Rect);

                                    lines.Add((line, bounds));
                                }
                            }
                            lineWords.Clear();
                        }
                    }
                    while (iter.Next(PageIteratorLevel.Word));

                    string text = MergeLines(lines);
                    return new OcrOutput
                    {
                        Text = text,
                        CharCount = text.Count(char.IsLetterOrDigit),
                        MeanConfidence = confidenceChars == 0 ? 0 : confidenceSum / confidenceChars
                    };
                }
            }
        }

        // ------------------------------------------------------------------
        // Text clean-up
        // ------------------------------------------------------------------

        private static bool IsUsableWord(string word, float confidence)
        {
            if (string.IsNullOrEmpty(word)) return false;
            if (!word.Any(char.IsLetterOrDigit)) return false;          // pure symbols are never text
            if (confidence < MinWordConfidence) return false;
            if (word.Length == 1 && confidence < 60 && !char.IsDigit(word[0])) return false; // lone shaky glyph
            return true;
        }

        private static bool IsReadableLine(string line)
        {
            int total = 0, alphanumeric = 0;
            foreach (char c in line)
            {
                if (char.IsWhiteSpace(c)) continue;
                total++;
                if (char.IsLetterOrDigit(c)) alphanumeric++;
            }
            return total > 0 && alphanumeric / (double)total >= 0.55;
        }

        /// <summary>
        /// Gentle clean-up. It only normalises whitespace and quotes and trims stray border glyphs; it never
        /// deletes real words, numbers, accents or Cyrillic text.
        /// </summary>
        public static string CleanLine(string line)
        {
            line = line.Replace('\u2018', '\'').Replace('\u2019', '\'')
                       .Replace('\u201C', '"').Replace('\u201D', '"');
            line = Regex.Replace(line, @"[\u0000-\u001F\u007F]", " ");
            line = Regex.Replace(line, @"^[\|~_\\\^`\[\]{}]+\s*|\s*[\|~_\\\^`\[\]{}]+$", "");
            line = Regex.Replace(line, @"[ \t]+", " ").Trim();
            return line;
        }

        public static string MergeLines(List<(string Text, Tesseract.Rect Bounds)> lines)
        {
            if (lines.Count == 0)
                return "";

            var result = new StringBuilder(lines[0].Text);

            for (int i = 1; i < lines.Count; i++)
            {
                var previous = lines[i - 1];
                var current = lines[i];

                int referenceHeight = Math.Max(previous.Bounds.Height, current.Bounds.Height);
                int verticalGap = current.Bounds.Y1 - previous.Bounds.Y2;

                bool looksLikeNewBlock = referenceHeight > 0 && verticalGap > referenceHeight * 0.7;
                bool previousEndsSentence = Regex.IsMatch(previous.Text, @"[.!?:;…]$|[.!?]['""”’)]$");
                bool hyphenated = !looksLikeNewBlock
                                  && previous.Text.Length > 1
                                  && previous.Text.EndsWith("-")
                                  && char.IsLetter(previous.Text[previous.Text.Length - 2])
                                  && char.IsLower(current.Text[0]);

                if (hyphenated)
                {
                    result.Length -= 1;           // "trans-" + "lation" -> "translation"
                    result.Append(current.Text);
                }
                else if (previousEndsSentence || looksLikeNewBlock)
                {
                    result.Append('\n').Append(current.Text);
                }
                else
                {
                    result.Append(' ').Append(current.Text);
                }
            }

            return result.ToString();
        }

        // ------------------------------------------------------------------
        // Image preprocessing
        // ------------------------------------------------------------------

        private static double ChooseScale(int width, int height)
        {
            int longest = Math.Max(width, height);
            double scale = longest <= 800 ? 3.0 : longest <= 1400 ? 2.0 : longest <= 2200 ? 1.5 : 1.0;

            while (scale > 1.0 && (width * scale) * (height * scale) > 5_000_000)
                scale -= 0.25;

            return Math.Max(1.0, scale);
        }

        private static GrayImage ToUpscaledGray(Bitmap source)
        {
            double scale = ChooseScale(source.Width, source.Height);
            int w = Math.Max(1, (int)Math.Round(source.Width * scale));
            int h = Math.Max(1, (int)Math.Round(source.Height * scale));

            using (var scaled = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(scaled))
                using (var attributes = new System.Drawing.Imaging.ImageAttributes())
                {
                    attributes.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.DrawImage(source, new Rectangle(0, 0, w, h), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
                }

                var data = scaled.LockBits(new Rectangle(0, 0, w, h),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                try
                {
                    int stride = data.Stride;
                    var raw = new byte[Math.Abs(stride) * h];
                    Marshal.Copy(data.Scan0, raw, 0, raw.Length);

                    var pixels = new byte[w * h];
                    for (int y = 0; y < h; y++)
                    {
                        int rowStart = y * stride;
                        int outStart = y * w;
                        for (int x = 0; x < w; x++)
                        {
                            int i = rowStart + x * 3; // B, G, R
                            pixels[outStart + x] = (byte)((29 * raw[i] + 150 * raw[i + 1] + 77 * raw[i + 2]) >> 8);
                        }
                    }

                    return new GrayImage { Pixels = pixels, Width = w, Height = h };
                }
                finally
                {
                    scaled.UnlockBits(data);
                }
            }
        }

        private static Bitmap ToPaddedBitmap(byte[] pixels, int width, int height, int padding)
        {
            int bw = width + padding * 2;
            int bh = height + padding * 2;

            var bitmap = new Bitmap(bw, bh, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            var data = bitmap.LockBits(new Rectangle(0, 0, bw, bh),
                System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            try
            {
                int stride = data.Stride;
                var buffer = new byte[Math.Abs(stride) * bh];
                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = 255; // white margin: Tesseract reads text touching the image edge poorly

                for (int y = 0; y < height; y++)
                {
                    int rowStart = (y + padding) * stride + padding * 3;
                    int srcStart = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        byte v = pixels[srcStart + x];
                        int o = rowStart + x * 3;
                        buffer[o] = v;
                        buffer[o + 1] = v;
                        buffer[o + 2] = v;
                    }
                }

                Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            bitmap.SetResolution(300, 300);
            return bitmap;
        }

        private static bool IsDarkBackground(byte[] pixels)
        {
            var histogram = new int[256];
            foreach (byte p in pixels) histogram[p]++;

            int threshold = ComputeOtsuThreshold(histogram, pixels.Length);

            long dark = 0;
            for (int t = 0; t <= threshold; t++) dark += histogram[t];

            // The background is the larger of the two groups.
            return dark > pixels.Length / 2;
        }

        private static void Invert(byte[] pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = (byte)(255 - pixels[i]);
        }

        private static void StretchContrast(byte[] pixels)
        {
            var histogram = new int[256];
            foreach (byte p in pixels) histogram[p]++;

            long lowTarget = pixels.Length / 100;
            long highTarget = pixels.Length - lowTarget;
            long cumulative = 0;
            int low = 0, high = 255;
            bool lowFound = false;

            for (int v = 0; v < 256; v++)
            {
                cumulative += histogram[v];
                if (!lowFound && cumulative > lowTarget) { low = v; lowFound = true; }
                if (cumulative >= highTarget) { high = v; break; }
            }

            if (high - low < 8)
                return; // practically flat: nothing to stretch

            double factor = 255.0 / (high - low);
            for (int i = 0; i < pixels.Length; i++)
            {
                double v = (pixels[i] - low) * factor;
                pixels[i] = (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
            }
        }

        /// <summary>Local mean thresholding via an integral image: a pixel is ink when it is clearly darker than its surroundings.</summary>
        private static byte[] AdaptiveThreshold(GrayImage image)
        {
            int w = image.Width, h = image.Height;
            byte[] src = image.Pixels;
            int stride = w + 1;

            var integral = new long[stride * (h + 1)];
            for (int y = 0; y < h; y++)
            {
                long rowSum = 0;
                for (int x = 0; x < w; x++)
                {
                    rowSum += src[y * w + x];
                    integral[(y + 1) * stride + (x + 1)] = integral[y * stride + (x + 1)] + rowSum;
                }
            }

            int radius = Math.Max(15, Math.Min(40, Math.Min(w, h) / 4));
            var result = new byte[w * h];

            for (int y = 0; y < h; y++)
            {
                int y0 = Math.Max(0, y - radius);
                int y1 = Math.Min(h - 1, y + radius);

                for (int x = 0; x < w; x++)
                {
                    int x0 = Math.Max(0, x - radius);
                    int x1 = Math.Min(w - 1, x + radius);

                    long sum = integral[(y1 + 1) * stride + (x1 + 1)]
                             - integral[y0 * stride + (x1 + 1)]
                             - integral[(y1 + 1) * stride + x0]
                             + integral[y0 * stride + x0];

                    double mean = (double)sum / ((x1 - x0 + 1) * (y1 - y0 + 1));
                    result[y * w + x] = src[y * w + x] < mean * 0.88 ? (byte)0 : (byte)255;
                }
            }

            return result;
        }

        public static int ComputeOtsuThreshold(int[] histogram, int totalPixels)
        {
            double sum = 0;
            for (int t = 0; t < 256; t++) sum += t * (double)histogram[t];

            double sumBackground = 0;
            long weightBackground = 0;
            double maxVariance = 0;
            int threshold = 128;

            for (int t = 0; t < 256; t++)
            {
                weightBackground += histogram[t];
                if (weightBackground == 0) continue;

                long weightForeground = totalPixels - weightBackground;
                if (weightForeground == 0) break;

                sumBackground += t * (double)histogram[t];
                double meanBackground = sumBackground / weightBackground;
                double meanForeground = (sum - sumBackground) / weightForeground;
                double diff = meanBackground - meanForeground;
                double variance = weightBackground * (double)weightForeground * diff * diff;

                if (variance > maxVariance)
                {
                    maxVariance = variance;
                    threshold = t;
                }
            }

            return threshold;
        }

        public void Dispose()
        {
            ocrLock.Wait(2000);
            try
            {
                ocrEngine?.Dispose();
                ocrEngine = null;
            }
            finally
            {
                ocrLock.Release();
            }

            (googleTranslator as IDisposable)?.Dispose();
            (yandexTranslator as IDisposable)?.Dispose();
        }
    }
}