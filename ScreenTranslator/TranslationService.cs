using GTranslate.Translators;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tesseract;

namespace ScreenTranslator
{
    public class TranslationResult
    {
        public string ExtractedText { get; set; } = "";
        public string TranslatedText { get; set; } = "";
        public bool HasContent => !string.IsNullOrWhiteSpace(TranslatedText);
    }

    public class TranslationService : IDisposable
    {
        public const string AllowedChars =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,?!'-" +
            "üğışçöÜĞİŞÇÖ" +
            "абвгдежзийклмнопрстуфхцчшщъыьэюяАБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯёЁ";

        public const float MinLineConfidence = 35f;

        private TesseractEngine ocrEngine;
        private string ocrEngineLang;

        public async Task<TranslationResult> TranslateCaptureAsync(
            Bitmap capturedImage, string sourceLang, string targetLang, string tessLang, string selectedApi, bool autoDetectSource = false)
        {
            var engine = GetOcrEngine(tessLang);

            string extractedText = RunOcr(engine, capturedImage);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                using (Bitmap thresholded = ApplyAdaptiveThreshold(capturedImage))
                {
                    extractedText = RunOcr(engine, thresholded);
                }
            }

            var result = new TranslationResult { ExtractedText = extractedText };

            if (string.IsNullOrWhiteSpace(extractedText) || extractedText.Length <= 2)
                return result;

            string effectiveSourceLang = autoDetectSource ? null : sourceLang;

            if (selectedApi == "Google")
            {
                var googleTranslator = new GoogleTranslator();
                var translation = await googleTranslator.TranslateAsync(extractedText, targetLang, effectiveSourceLang);
                result.TranslatedText = translation.Translation;
            }
            else
            {
                var yandexTranslator = new YandexTranslator();
                var translation = await yandexTranslator.TranslateAsync(extractedText, targetLang, effectiveSourceLang);
                result.TranslatedText = translation.Translation;
            }

            return result;
        }

        private TesseractEngine GetOcrEngine(string tessLang)
        {
            if (ocrEngine == null || ocrEngineLang != tessLang)
            {
                ocrEngine?.Dispose();
                string tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                ocrEngine = new TesseractEngine(tessDataPath, tessLang, EngineMode.LstmOnly);
                ocrEngine.SetVariable("tessedit_char_whitelist", AllowedChars);
                ocrEngineLang = tessLang;
            }
            return ocrEngine;
        }

        private static string RunOcr(TesseractEngine engine, Bitmap bitmap)
        {
            byte[] imageBytes;
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                imageBytes = stream.ToArray();
            }

            using (var img = Pix.LoadFromMemory(imageBytes))
            using (var page = engine.Process(img, PageSegMode.SingleBlock))
            using (var iter = page.GetIterator())
            {
                iter.Begin();
                var lines = new List<(string Text, Tesseract.Rect Bounds)>();

                do
                {
                    string lineText = iter.GetText(PageIteratorLevel.TextLine);
                    float confidence = iter.GetConfidence(PageIteratorLevel.TextLine);

                    if (string.IsNullOrWhiteSpace(lineText) || confidence < MinLineConfidence)
                        continue;

                    string cleaned = CleanLine(lineText);
                    if (cleaned.Length == 0)
                        continue;

                    if (iter.TryGetBoundingBox(PageIteratorLevel.TextLine, out Tesseract.Rect bounds))
                        lines.Add((cleaned, bounds));
                }
                while (iter.Next(PageIteratorLevel.TextLine));

                return MergeLines(lines);
            }
        }

        public static string CleanLine(string line)
        {
            line = Regex.Replace(line, @"[^a-zA-Z0-9\s.,?!'üğışçöÜĞİŞÇÖа-яА-ЯёЁ-]", "");
            line = Regex.Replace(line, @"\b[^a-eıioöuüA-EIIOÖUÜ\s]{2,5}\b", "", RegexOptions.IgnoreCase);
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
                int previousLineHeight = lines[i - 1].Bounds.Height;
                int verticalGap = lines[i].Bounds.Y1 - lines[i - 1].Bounds.Y2;

                bool previousEndsSentence = Regex.IsMatch(lines[i - 1].Text, @"[.!?:]$");
                bool looksLikeNewBlock = verticalGap > previousLineHeight * 1.1;

                if (previousEndsSentence || looksLikeNewBlock)
                    result.Append('\n').Append(lines[i].Text);
                else
                    result.Append(' ').Append(lines[i].Text);
            }

            return result.ToString();
        }

        private static Bitmap ApplyAdaptiveThreshold(Bitmap original)
        {
            var gray = new Bitmap(original.Width, original.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(gray))
            {
                var colorMatrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
                {
                    new float[] {0.299f, 0.299f, 0.299f, 0, 0},
                    new float[] {0.587f, 0.587f, 0.587f, 0, 0},
                    new float[] {0.114f, 0.114f, 0.114f, 0, 0},
                    new float[] {0, 0, 0, 1, 0},
                    new float[] {0, 0, 0, 0, 1}
                });
                var attributes = new System.Drawing.Imaging.ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);
                g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
                    0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
            }

            var data = gray.LockBits(new Rectangle(0, 0, gray.Width, gray.Height),
                System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            int byteCount = data.Stride * gray.Height;
            byte[] buffer = new byte[byteCount];
            Marshal.Copy(data.Scan0, buffer, 0, byteCount);

            int[] histogram = new int[256];
            for (int i = 0; i < byteCount; i += 3)
                histogram[buffer[i]]++;

            int threshold = ComputeOtsuThreshold(histogram, gray.Width * gray.Height);

            for (int i = 0; i < byteCount; i += 3)
            {
                byte value = buffer[i] > threshold ? (byte)255 : (byte)0;
                buffer[i] = buffer[i + 1] = buffer[i + 2] = value;
            }

            Marshal.Copy(buffer, 0, data.Scan0, byteCount);
            gray.UnlockBits(data);
            return gray;
        }

        public static int ComputeOtsuThreshold(int[] histogram, int totalPixels)
        {
            float sum = 0;
            for (int t = 0; t < 256; t++) sum += t * histogram[t];

            float sumB = 0;
            int weightBackground = 0;
            float maxVariance = 0;
            int threshold = 128;

            for (int t = 0; t < 256; t++)
            {
                weightBackground += histogram[t];
                if (weightBackground == 0) continue;

                int weightForeground = totalPixels - weightBackground;
                if (weightForeground == 0) break;

                sumB += t * histogram[t];
                float meanBackground = sumB / weightBackground;
                float meanForeground = (sum - sumB) / weightForeground;
                float variance = weightBackground * (float)weightForeground * (meanBackground - meanForeground) * (meanBackground - meanForeground);

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
            ocrEngine?.Dispose();
        }
    }
}