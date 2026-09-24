using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Dsp;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using DWriteFontStyle = SharpDX.DirectWrite.FontStyle;
using DWriteFontWeight = SharpDX.DirectWrite.FontWeight;
using Factory2D = SharpDX.Direct2D1.Factory;
using FactoryDW = SharpDX.DirectWrite.Factory;
using TextAntialiasMode = SharpDX.Direct2D1.TextAntialiasMode;
using D2DBitmap = SharpDX.Direct2D1.Bitmap;
using PixelFormat = SharpDX.Direct2D1.PixelFormat;

namespace AmySonicVisualizer.VisualizerControls
{
    public class SpectrogramVisualizerControl : BaseVisualizerControl
    {
        private const int FftSize = 4096;
        private const int FftBits = 12;

        [Category("Spectrogram Settings")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public double MinFreq { get; set; } = 32.7;

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
        public double[] ScaleFrequencies { get; set; } = { 50, 100, 200, 500, 1000, 2000, 5000, 10000 };

        private bool _revealAll = false;
        private double _gainOffset = 0.0;
        private bool _isProcessing = false;
        private string _statusMessage = "Press [Ctrl+O] or Click Here to Load Audio Data";

        private double[,]? _dbCache;
        private int _cachedWidth;
        private int _cachedHeight;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool RevealAll
        {
            get => _revealAll;
            set { _revealAll = value; Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double GainOffset
        {
            get => _gainOffset;
            set { _gainOffset = value; ReapplyColorsD2D(); }
        }

        private Factory2D? _factory2D;
        private FactoryDW? _factoryDW;
        private WindowRenderTarget? _renderTarget;
        private D2DBitmap? _d2dSpectrogramBitmap;

        // Brushes
        private SolidColorBrush? _unrevealedBrush;
        private SolidColorBrush? _cursorLineBrush;
        private SolidColorBrush? _scaleBgBrush;
        private SolidColorBrush? _whiteKeyBrush;
        private SolidColorBrush? _blackKeyBrush;
        private SolidColorBrush? _cKeyBrush;
        private SolidColorBrush? _cKeyTextBrush;
        private SolidColorBrush? _keyBorderBrush;
        private SolidColorBrush? _tickBrush;
        private SolidColorBrush? _textBrush;
        private SolidColorBrush? _statusBrush;
        private SolidColorBrush? _gainBrush;

        // Text Formats
        private TextFormat? _statusTextFormat;
        private TextFormat? _scaleTextFormat;
        private TextFormat? _gainTextFormat;

        public SpectrogramVisualizerControl()
        {
            // Optimize WinForms control flags for custom hardware rendering
            SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
            DoubleBuffered = false;

            BackColor = System.Drawing.Color.FromArgb(15, 15, 18);
            MouseDown += OnMouseDown;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!DesignMode) InitDirect2D();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_renderTarget != null)
            {
                _renderTarget.Resize(new Size2(Width, Height));
                Invalidate();
            }
        }

        private void InitDirect2D()
        {
            _factory2D = new Factory2D();
            _factoryDW = new FactoryDW();

            var properties = new HwndRenderTargetProperties
            {
                Hwnd = this.Handle,
                PixelSize = new Size2(Width, Height),
                PresentOptions = PresentOptions.Immediately
            };

            _renderTarget = new WindowRenderTarget(
                _factory2D,
                new RenderTargetProperties(new PixelFormat(SharpDX.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied)),
                properties
            );

            _renderTarget.TextAntialiasMode = TextAntialiasMode.Cleartype;
            _renderTarget.AntialiasMode = AntialiasMode.PerPrimitive;

            // Initialize Brushes
            _unrevealedBrush = new SolidColorBrush(_renderTarget, new RawColor4(10f / 255f, 10f / 255f, 14f / 255f, 1f));
            _cursorLineBrush = new SolidColorBrush(_renderTarget, new RawColor4(235f / 255f, 235f / 255f, 245f / 255f, 1f));
            _scaleBgBrush = new SolidColorBrush(_renderTarget, new RawColor4(10f / 255f, 10f / 255f, 14f / 255f, 180f / 255f));

            _whiteKeyBrush = new SolidColorBrush(_renderTarget, new RawColor4(220f / 255f, 220f / 255f, 225f / 255f, 1f));
            _blackKeyBrush = new SolidColorBrush(_renderTarget, new RawColor4(25f / 255f, 25f / 255f, 30f / 255f, 1f));
            _cKeyBrush = new SolidColorBrush(_renderTarget, new RawColor4(110f / 255f, 20f / 255f, 40f / 255f, 1f));
            _cKeyTextBrush = new SolidColorBrush(_renderTarget, new RawColor4(255f / 255f, 120f / 255f, 130f / 255f, 1f));
            _keyBorderBrush = new SolidColorBrush(_renderTarget, new RawColor4(10f / 255f, 10f / 255f, 14f / 255f, 1f));

            _tickBrush = new SolidColorBrush(_renderTarget, new RawColor4(50f / 255f, 50f / 255f, 60f / 255f, 1f));
            _textBrush = new SolidColorBrush(_renderTarget, new RawColor4(100f / 255f, 100f / 255f, 110f / 255f, 1f));
            _statusBrush = new SolidColorBrush(_renderTarget, new RawColor4(180f / 255f, 180f / 255f, 190f / 255f, 1f));
            _gainBrush = new SolidColorBrush(_renderTarget, new RawColor4(235f / 255f, 235f / 255f, 245f / 255f, 1f));

            // Initialize TextFormats
            _statusTextFormat = new TextFormat(_factoryDW, "Segoe UI", DWriteFontWeight.Normal, DWriteFontStyle.Normal, 12f)
            {
                TextAlignment = SharpDX.DirectWrite.TextAlignment.Center,
                ParagraphAlignment = ParagraphAlignment.Center
            };

            _scaleTextFormat = new TextFormat(_factoryDW, "Consolas", DWriteFontWeight.Normal, DWriteFontStyle.Normal, 8.5f)
            {
                TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading,
                ParagraphAlignment = ParagraphAlignment.Center
            };

            _gainTextFormat = new TextFormat(_factoryDW, "Consolas", DWriteFontWeight.Bold, DWriteFontStyle.Normal, 12f)
            {
                TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading,
                ParagraphAlignment = ParagraphAlignment.Near
            };
        }

        private void CleanupDirect2D()
        {
            _d2dSpectrogramBitmap?.Dispose();
            _unrevealedBrush?.Dispose();
            _cursorLineBrush?.Dispose();
            _scaleBgBrush?.Dispose();
            _whiteKeyBrush?.Dispose();
            _blackKeyBrush?.Dispose();
            _cKeyBrush?.Dispose();
            _cKeyTextBrush?.Dispose();
            _keyBorderBrush?.Dispose();
            _tickBrush?.Dispose();
            _textBrush?.Dispose();
            _statusBrush?.Dispose();
            _gainBrush?.Dispose();

            _statusTextFormat?.Dispose();
            _scaleTextFormat?.Dispose();
            _gainTextFormat?.Dispose();

            _renderTarget?.Dispose();
            _factoryDW?.Dispose();
            _factory2D?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) CleanupDirect2D();
            base.Dispose(disposing);
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

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_isProcessing || _dbCache == null) return;
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

            if (Engine.IsLoaded && Width > 0)
            {
                double progress = Math.Clamp((double)e.X / Width, 0.0, 1.0);
                Engine.Progress = progress;
            }
        }

        private async Task RegenerateSpectrogramAsync()
        {
            if (Engine == null) return;

            int width = Math.Max(Width, 1);
            int height = Math.Max(Height, 1);

            float[] monoSamples = Engine.MonoSamples;
            int sampleRate = Engine.SampleRate;

            try
            {
                _dbCache = await Task.Run(() => GenerateSpectrogramDbCache(monoSamples, sampleRate, width, height));
                _cachedWidth = width;
                _cachedHeight = height;
                _gainOffset = 0.0;
                RevealAll = false;

                // Create the texture and upload the colors on the UI thread
                ReapplyColorsD2D();
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

        private double[,] GenerateSpectrogramDbCache(float[] monoSamples, int sampleRate, int width, int height)
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

            return dbCache;
        }

        private void ReapplyColorsD2D()
        {
            if (_dbCache == null || _renderTarget == null || DesignMode) return;

            int[] pixels = new int[_cachedWidth * _cachedHeight];
            double currentMinDb = MinDb - _gainOffset;
            double currentMaxDb = MaxDb - _gainOffset;

            for (int y = 0; y < _cachedHeight; y++)
            {
                int yOffset = y * _cachedWidth;
                for (int x = 0; x < _cachedWidth; x++)
                {
                    double db = _dbCache[x, y];
                    float norm = Math.Clamp((float)((db - currentMinDb) / (currentMaxDb - currentMinDb)), 0f, 1f);
                    pixels[yOffset + x] = GetSpectralColorInt(norm);
                }
            }

            // Create or resize the D2D texture if needed
            if (_d2dSpectrogramBitmap == null || _d2dSpectrogramBitmap.PixelSize.Width != _cachedWidth || _d2dSpectrogramBitmap.PixelSize.Height != _cachedHeight)
            {
                _d2dSpectrogramBitmap?.Dispose();
                var bmpProps = new BitmapProperties(new PixelFormat(SharpDX.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied));
                _d2dSpectrogramBitmap = new D2DBitmap(_renderTarget, new Size2(_cachedWidth, _cachedHeight), bmpProps);
            }

            // Copy populated pixel array memory to the hardware texture
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                _d2dSpectrogramBitmap.CopyFromMemory(handle.AddrOfPinnedObject(), _cachedWidth * 4);
            }
            finally
            {
                handle.Free();
            }

            Invalidate();
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

            // B8G8R8A8_UNorm memory packing translates seamlessly with typical integer packing 
            // since little endian stores the least significant byte first.
            return (255 << 24) | (r << 16) | (g << 8) | b;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (DesignMode) base.OnPaintBackground(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (DesignMode)
            {
                e.Graphics.Clear(BackColor);
                e.Graphics.DrawString("Direct2D Spectrogram Control (Designer Mode)", new System.Drawing.Font("Segoe UI", 10), System.Drawing.Brushes.White, 10, 10);
                return;
            }

            if (Bypass || _renderTarget == null) return;
            RenderD2D();
        }

        private void RenderD2D()
        {
            _renderTarget.BeginDraw();
            _renderTarget.Clear(new RawColor4(15f / 255f, 15f / 255f, 18f / 255f, 1f));

            if (_isProcessing || _d2dSpectrogramBitmap == null || Engine == null || !Engine.IsLoaded)
            {
                if (_statusTextFormat != null && _statusBrush != null)
                {
                    _renderTarget.DrawText(_statusMessage, _statusTextFormat, new RawRectangleF(0, 0, Width, Height), _statusBrush);
                }
                _renderTarget.EndDraw();
                return;
            }

            double progress = Engine.Progress;
            float currentX = (float)(progress * Width);
            float windowDrawWidth = _revealAll ? Width : currentX;

            float bitmapCurrentX = (float)(progress * _d2dSpectrogramBitmap.PixelSize.Width);
            float bitmapDrawWidth = _revealAll ? _d2dSpectrogramBitmap.PixelSize.Width : bitmapCurrentX;

            if (windowDrawWidth > 0 && bitmapDrawWidth > 0)
            {
                var destRect = new RawRectangleF(0, 0, windowDrawWidth, Height);
                var srcRect = new RawRectangleF(0, 0, bitmapDrawWidth, _d2dSpectrogramBitmap.PixelSize.Height);
                _renderTarget.DrawBitmap(_d2dSpectrogramBitmap, destRect, 1.0f, BitmapInterpolationMode.Linear, srcRect);
            }

            if (!_revealAll && currentX < Width)
            {
                _renderTarget.FillRectangle(new RawRectangleF(currentX, 0, Width, Height), _unrevealedBrush);
            }

            _renderTarget.DrawLine(new RawVector2(currentX, 0), new RawVector2(currentX, Height), _cursorLineBrush, 1.5f);

            DrawScaleOverlayD2D(currentX);

            if (_gainOffset != 0.0 && _gainTextFormat != null && _gainBrush != null)
            {
                string sign = _gainOffset > 0 ? "+" : "";
                var textRect = new RawRectangleF(15, 15, 200, 50);
                _renderTarget.DrawText($"Gain: {sign}{_gainOffset:F1} dB", _gainTextFormat, textRect, _gainBrush);
            }

            _renderTarget.EndDraw();
        }

        private void DrawScaleOverlayD2D(float currentX)
        {
            if (_scaleBgBrush == null || _whiteKeyBrush == null || _blackKeyBrush == null || _cKeyBrush == null || _keyBorderBrush == null) return;

            float scaleBoxX = currentX + 2;
            float pianoWidth = 12;
            float textWidth = 40; // Broadened slightly to comfortably fit octave numbers alongside frequency values
            float totalScaleWidth = pianoWidth + textWidth + 5;

            // Draw Background Panel
            _renderTarget.FillRectangle(new RawRectangleF(scaleBoxX, 0, scaleBoxX + totalScaleWidth, Height), _scaleBgBrush);

            float pianoX = scaleBoxX;
            float textX = pianoX + pianoWidth + 4;

            // Draw Piano Keys and Contextual Octave Labels
            for (int n = 12; n <= 127; n++)
            {
                double freqTop = 440.0 * Math.Pow(2.0, (n - 69 + 0.5) / 12.0);
                double freqBottom = 440.0 * Math.Pow(2.0, (n - 69 - 0.5) / 12.0);

                if (freqTop < MinFreq || freqBottom > MaxFreq) continue;

                float yTop = GetYForFrequency(freqTop, Height);
                float yBottom = GetYForFrequency(freqBottom, Height);
                float keyHeight = Math.Max(1f, yBottom - yTop);

                bool isC = (n % 12 == 0);
                bool isBlack = (n % 12 == 1 || n % 12 == 3 || n % 12 == 6 || n % 12 == 8 || n % 12 == 10);

                SharpDX.Direct2D1.Brush b = isC ? _cKeyBrush : (isBlack ? _blackKeyBrush : _whiteKeyBrush);
                var keyRect = new RawRectangleF(pianoX, yTop, pianoX + pianoWidth, yTop + keyHeight);

                _renderTarget.FillRectangle(keyRect, b);
                _renderTarget.DrawRectangle(keyRect, _keyBorderBrush, 1f);

                // Add C-key octave labels (e.g. C3, C4) precisely centered vertically next to the C keys
                if (isC && _scaleTextFormat != null && _cKeyTextBrush != null)
                {
                    int octave = (n / 12) - 1; // Standard scientific pitch mapping (Note 60 = Middle C = C4)
                    string cLabel = $"C{octave}";
                    float yCenter = (yTop + yBottom) / 2f;

                    // Box is aligned dead center along the key's height constraint using ParagraphAlignment.Center
                    var cTextRect = new RawRectangleF(textX, yCenter - 10, textX + textWidth, yCenter + 10);
                    _renderTarget.DrawText(cLabel, _scaleTextFormat, cTextRect, _cKeyTextBrush);
                }
            }

            // Draw Scale Text and Ticks
            if (_scaleTextFormat != null && _textBrush != null && _tickBrush != null)
            {
                const float ScaleTextOffsetX = 20f;
                foreach (double freq in ScaleFrequencies)
                {
                    if (freq < MinFreq || freq > MaxFreq) continue;
                    float y = GetYForFrequency(freq, Height);

                    _renderTarget.DrawLine(new RawVector2(pianoX + pianoWidth, y), new RawVector2(textX + textWidth - 5, y), _tickBrush, 1f);

                    string label = freq >= 1000 ? $"{(freq / 1000)}k" : freq.ToString();
                    var textRect = new RawRectangleF(textX + ScaleTextOffsetX, y - 10, textX + textWidth + ScaleTextOffsetX, y + 10);
                    _renderTarget.DrawText(label, _scaleTextFormat, textRect, _textBrush);
                }
            }
        }

        private float GetYForFrequency(double freq, float height)
        {
            freq = Math.Clamp(freq, MinFreq, MaxFreq);
            double normY = Math.Log(freq / MinFreq) / Math.Log(MaxFreq / MinFreq);
            return (float)(height - 1 - (normY * height));
        }
    }
}