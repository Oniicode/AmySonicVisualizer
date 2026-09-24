using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Dsp;

namespace AmySonicVisualizer
{
    public class SpectrogramControl : VisualizerControlBase
    {
        private Bitmap? _spectrogramBitmap;
        private double[,]? _dbCache;

        private bool _isProcessing = false;
        private string _statusMessage = "Press [Ctrl+O] or Click to Open Audio File";

        // Internal state backing the public properties
        private bool _revealAll = false;
        private double _gainOffset = 0.0;

        private const int FftSize = 4096;
        private const int FftBits = 12; // 2^12 = 4096

        [Category("Spectrogram Settings")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public double MinFreq { get; set; } = 40.0;

        [Category("Spectrogram Settings")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public double MaxFreq { get; set; } = 8000.0;

        [Category("Spectrogram Settings")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public double MinDb { get; set; } = -75.0;

        [Category("Spectrogram Settings")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public double MaxDb { get; set; } = -5.0;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double[] ScaleFrequencies { get; set; } = { 40, 50, 100, 200, 500, 1000, 2000, 5000, 8000 };

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool RevealAll
        {
            get => _revealAll;
            set
            {
                _revealAll = value;
                Invalidate();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double GainOffset
        {
            get => _gainOffset;
            set
            {
                _gainOffset = value;
                ReapplyColors();
            }
        }

        public SpectrogramControl()
        {
            BackColor = Color.FromArgb(15, 15, 18);
            MouseDown += OnMouseDown;
        }

        public override void Bind(AudioEngine engine)
        {
            base.Bind(engine);

            engine.AudioLoading += (s, e) =>
            {
                _isProcessing = true;
                _statusMessage = "Loading audio track...";
                Invalidate();
            };

            engine.FileLoaded += async (s, e) =>
            {
                _statusMessage = "Analyzing melodic frequencies across track...";
                Invalidate();
                await RegenerateSpectrogramAsync();
            };
        }

        private async Task RegenerateSpectrogramAsync()
        {
            if (Engine == null) return;

            int width = Math.Max(ClientSize.Width, 1);
            int height = Math.Max(ClientSize.Height, 1);

            float[] monoSamples = Engine.MonoSamples;
            int sampleRate = Engine.SampleRate;

            try
            {
                var result = await Task.Run(() => GenerateSpectrogram(monoSamples, sampleRate, width, height));

                _spectrogramBitmap?.Dispose();
                _spectrogramBitmap = result.Bmp;
                _dbCache = result.DbCache;
                _gainOffset = 0.0;
                RevealAll = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate spectrogram: {ex.Message}");
                _statusMessage = "Press [O] or Click to Open Audio File";
            }
            finally
            {
                _isProcessing = false;
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if (_isProcessing || _spectrogramBitmap == null || _dbCache == null) return;
            double gainChange = (e.Delta / 120.0) * 2.5;
            GainOffset += gainChange;
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (Engine == null) return;

            if (!Engine.IsLoaded && !_isProcessing)
            {
                string? path = Program.PromptOpenFile();
                if (!string.IsNullOrEmpty(path))
                {
                    _ = Engine.LoadAsync(path);
                }
                return;
            }

            if (Engine.IsLoaded && ClientSize.Width > 0)
            {
                double progress = Math.Clamp((double)e.X / ClientSize.Width, 0.0, 1.0);
                Engine.Progress = progress;
            }
        }

        private void ReapplyColors()
        {
            if (_spectrogramBitmap != null && _dbCache != null)
            {
                ApplyColorsToBitmap(_spectrogramBitmap, _dbCache, _spectrogramBitmap.Width, _spectrogramBitmap.Height, _gainOffset);
                Invalidate();
            }
        }

        private (Bitmap Bmp, double[,] DbCache) GenerateSpectrogram(float[] monoSamples, int sampleRate, int width, int height)
        {
            var complexBuffer = new Complex[FftSize];
            double[,] dbCache = new double[width, height];

            int[] rowToBin = new int[height];
            for (int y = 0; y < height; y++)
            {
                double normY = (double)(height - 1 - y) / height;
                double freq = MinFreq * Math.Pow(MaxFreq / MinFreq, normY);
                int bin = (int)Math.Round(freq * FftSize / sampleRate);
                rowToBin[y] = Math.Clamp(bin, 0, (FftSize / 2) - 1);
            }

            for (int x = 0; x < width; x++)
            {
                long centerSample = (long)x * monoSamples.Length / width;
                long startSample = centerSample - (FftSize / 2);

                for (int i = 0; i < FftSize; i++)
                {
                    long sampleIdx = startSample + i;
                    float sampleVal = (sampleIdx >= 0 && sampleIdx < monoSamples.Length) ? monoSamples[sampleIdx] : 0f;
                    float windowMultiplier = (float)FastFourierTransform.HannWindow(i, FftSize);
                    complexBuffer[i].X = sampleVal * windowMultiplier;
                    complexBuffer[i].Y = 0f;
                }

                FastFourierTransform.FFT(true, FftBits, complexBuffer);

                for (int y = 0; y < height; y++)
                {
                    int bin = rowToBin[y];
                    double real = complexBuffer[bin].X;
                    double imag = complexBuffer[bin].Y;
                    double mag = Math.Sqrt(real * real + imag * imag);
                    dbCache[x, y] = 20.0 * Math.Log10(Math.Max(mag, 1e-6));
                }
            }

            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            ApplyColorsToBitmap(bmp, dbCache, width, height, 0.0);
            return (bmp, dbCache);
        }

        private void ApplyColorsToBitmap(Bitmap bmp, double[,] dbCache, int width, int height, double gainOffset)
        {
            int[] pixels = new int[width * height];
            double currentMinDb = MinDb - gainOffset;
            double currentMaxDb = MaxDb - gainOffset;

            for (int y = 0; y < height; y++)
            {
                int yOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    double db = dbCache[x, y];
                    float norm = Math.Clamp((float)((db - currentMinDb) / (currentMaxDb - currentMinDb)), 0f, 1f);
                    pixels[yOffset + x] = GetSpectralColorInt(norm);
                }
            }

            var bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
            bmp.UnlockBits(bmpData);
        }

        private static int GetSpectralColorInt(float intensity)
        {
            if (intensity <= 0.0f) return (255 << 24) | (12 << 16) | (10 << 8) | 18;

            int r, g, b;
            if (intensity < 0.25f)
            {
                float t = intensity / 0.25f;
                r = (int)(45 * t); g = 0; b = (int)(90 * t);
            }
            else if (intensity < 0.55f)
            {
                float t = (intensity - 0.25f) / 0.30f;
                r = (int)(45 + (180 - 45) * t); g = (int)(15 * t); b = (int)(90 + (130 - 90) * t);
            }
            else if (intensity < 0.85f)
            {
                float t = (intensity - 0.55f) / 0.30f;
                r = (int)(180 + (40 - 180) * t); g = (int)(15 + (210 - 15) * t); b = (int)(220 + (255 - 220) * t);
            }
            else
            {
                float t = (intensity - 0.85f) / 0.15f;
                r = (int)(40 + (255 - 40) * t); g = (int)(210 + (255 - 210) * t); b = 255;
            }

            return (255 << 24) | (r << 16) | (g << 8) | b;
        }

        private int GetYForFrequency(double freq, int height)
        {
            freq = Math.Clamp(freq, MinFreq, MaxFreq);
            double normY = Math.Log(freq / MinFreq) / Math.Log(MaxFreq / MinFreq);
            return (int)Math.Round(height - 1 - (normY * height));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Bypass) return;
            base.OnPaint(e);

            if (_isProcessing || _spectrogramBitmap == null || Engine == null || !Engine.IsLoaded)
            {
                using var brush = new SolidBrush(Color.FromArgb(180, 180, 190));
                using var font = new Font("Segoe UI", 12f, FontStyle.Regular);
                var size = e.Graphics.MeasureString(_statusMessage, font);
                e.Graphics.DrawString(_statusMessage, font, brush, (ClientSize.Width - size.Width) / 2, (ClientSize.Height - size.Height) / 2);
                return;
            }

            double progress = Engine.Progress;

            int currentX = (int)(progress * ClientSize.Width);
            int windowDrawWidth = _revealAll ? ClientSize.Width : currentX;

            int bitmapCurrentX = (int)(progress * _spectrogramBitmap.Width);
            int bitmapDrawWidth = _revealAll ? _spectrogramBitmap.Width : bitmapCurrentX;

            if (windowDrawWidth > 0 && bitmapDrawWidth > 0)
            {
                var srcRect = new Rectangle(0, 0, bitmapDrawWidth, _spectrogramBitmap.Height);
                var destRect = new Rectangle(0, 0, windowDrawWidth, ClientSize.Height);
                e.Graphics.DrawImage(_spectrogramBitmap, destRect, srcRect, GraphicsUnit.Pixel);
            }

            if (!_revealAll && currentX < ClientSize.Width)
            {
                using var blackBrush = new SolidBrush(Color.FromArgb(10, 10, 14));
                e.Graphics.FillRectangle(blackBrush, currentX, 0, ClientSize.Width - currentX, ClientSize.Height);
            }

            using var linePen = new Pen(Color.FromArgb(235, 235, 245), 1.5f);
            e.Graphics.DrawLine(linePen, currentX, 0, currentX, ClientSize.Height);

            DrawScaleOverlay(e.Graphics, currentX);

            if (_gainOffset != 0.0)
            {
                using var gainFont = new Font("Consolas", 12f, FontStyle.Bold);
                using var gainBrush = new SolidBrush(Color.FromArgb(235, 235, 245));
                string sign = _gainOffset > 0 ? "+" : "";
                e.Graphics.DrawString($"Gain: {sign}{_gainOffset:F1} dB", gainFont, gainBrush, 15, 15);
            }
        }

        private void DrawScaleOverlay(Graphics g, int currentX)
        {
            int scaleBoxX = currentX + 2;
            int pianoWidth = 12;
            int textWidth = 35;
            int totalScaleWidth = pianoWidth + textWidth + 5;

            using var scaleBg = new SolidBrush(Color.FromArgb(210, 10, 10, 14));
            g.FillRectangle(scaleBg, scaleBoxX, 0, totalScaleWidth, ClientSize.Height);

            int pianoX = scaleBoxX;
            int textX = pianoX + pianoWidth + 4;

            using var whiteKeyBrush = new SolidBrush(Color.FromArgb(220, 220, 225));
            using var blackKeyBrush = new SolidBrush(Color.FromArgb(25, 25, 30));
            using var cKeyBrush = new SolidBrush(Color.FromArgb(110, 20, 40));
            using var keyBorderPen = new Pen(Color.FromArgb(10, 10, 14), 1f);

            for (int n = 12; n <= 127; n++)
            {
                double freqTop = 440.0 * Math.Pow(2.0, (n - 69 + 0.5) / 12.0);
                double freqBottom = 440.0 * Math.Pow(2.0, (n - 69 - 0.5) / 12.0);

                if (freqTop < MinFreq || freqBottom > MaxFreq) continue;

                int yTop = GetYForFrequency(freqTop, ClientSize.Height);
                int yBottom = GetYForFrequency(freqBottom, ClientSize.Height);
                int keyHeight = Math.Max(1, yBottom - yTop);

                bool isC = (n % 12 == 0);
                bool isBlack = (n % 12 == 1 || n % 12 == 3 || n % 12 == 6 || n % 12 == 8 || n % 12 == 10);

                Brush b = isC ? cKeyBrush : (isBlack ? blackKeyBrush : whiteKeyBrush);
                g.FillRectangle(b, pianoX, yTop, pianoWidth, keyHeight);
                g.DrawRectangle(keyBorderPen, pianoX, yTop, pianoWidth, keyHeight);
            }

            using var font = new Font("Consolas", 8.5f, FontStyle.Regular);
            using var textBrush = new SolidBrush(Color.FromArgb(200, 200, 200));
            using var tickPen = new Pen(Color.FromArgb(80, 80, 90), 1f);
            using var format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

            foreach (double freq in ScaleFrequencies)
            {
                if (freq < MinFreq || freq > MaxFreq) continue;
                int y = GetYForFrequency(freq, ClientSize.Height);

                g.DrawLine(tickPen, pianoX + pianoWidth, y, textX + textWidth - 5, y);

                string label = freq >= 1000 ? $"{(freq / 1000)}k" : freq.ToString();
                var textRect = new Rectangle(textX, y - 10, textWidth, 20);
                g.DrawString(label, font, textBrush, textRect, format);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _spectrogramBitmap?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}