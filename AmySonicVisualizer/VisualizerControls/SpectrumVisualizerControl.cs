using NAudio.Dsp;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using System.ComponentModel;
using DWriteFontStyle = SharpDX.DirectWrite.FontStyle;
using DWriteFontWeight = SharpDX.DirectWrite.FontWeight;
using Factory2D = SharpDX.Direct2D1.Factory;
using FactoryDW = SharpDX.DirectWrite.Factory;
using TextAntialiasMode = SharpDX.Direct2D1.TextAntialiasMode;

namespace AmySonicVisualizer.VisualizerControls
{
    public class SpectrumVisualizerControl : BaseVisualizerControl
    {
        private const float OverlayOpacity = 0.5f;

        [Category("FFT Settings")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public double MinFreq { get; set; } = 20.0;

        [Category("FFT Settings")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public double MaxFreq { get; set; } = 20000.0;

        [Category("FFT Settings")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public double MinDb { get; set; } = -75.0;

        [Category("FFT Settings")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public double MaxDb { get; set; } = -5.0;

        [Category("FFT Settings")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public double[] ScaleFrequencies { get; set; } = { 50, 100, 200, 500, 1000, 2000, 5000, 10000, 16000 };

        private bool _smoothForeground = true;

        [Category("FFT Settings")]
        [Description("Toggles between smooth (interpolated) and blocky rendering for the foreground graph.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        [DefaultValue(true)]
        public bool SmoothForeground
        {
            get => _smoothForeground;
            set
            {
                if (_smoothForeground != value)
                {
                    _smoothForeground = value;
                    Invalidate();
                }
            }
        }

        private int _fftSize = 8192;
        public int FftBits { get; private set; } = 13;

        public event EventHandler? FftSizeChanged;

        [Category("FFT Settings")]
        [Description("The size of the FFT window. Internally snaps to the nearest power of 2.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        [DefaultValue(8192)]
        public int FftSize
        {
            get => _fftSize;
            set
            {
                int clamped = Math.Clamp(value, 256, 32768);
                int validFftSize = (int)Math.Pow(2, Math.Round(Math.Log(clamped, 2)));

                if (_fftSize != validFftSize)
                {
                    _fftSize = validFftSize;
                    FftBits = (int)Math.Log(_fftSize, 2);

                    complexBuffer = new Complex[_fftSize];

                    FftSizeChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        private Factory2D factory2D;
        private FactoryDW factoryDW;
        private WindowRenderTarget renderTarget;

        private SolidColorBrush fillBrush;
        private SolidColorBrush lineBrush;
        private SolidColorBrush gridBrush;
        private SolidColorBrush textBrush;
        private SolidColorBrush bandBrush;
        private SolidColorBrush statusBrush;
        private SolidColorBrush blackTextBrush;
        private SolidColorBrush heatmapBrush;
        private SolidColorBrush hoverLineBrush;
        private SolidColorBrush hoverTextBrush;

        private SolidColorBrush blackKeyBrush;
        private SolidColorBrush cKeyBrush;
        private SolidColorBrush cKeyTextBrush;
        private TextFormat keyTextFormat;

        private TextFormat gridTextFormat;
        private TextFormat statusTextFormat;
        private TextFormat bandTextFormat;
        private TextFormat hoverTextFormat;

        private Complex[] complexBuffer;
        private RawVector2[] pointsBuffer;
        private RawVector2[] blockyPointsBuffer;

        public SpectrumVisualizerControl()
        {
            SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
            DoubleBuffered = false;

            BackColor = System.Drawing.Color.FromArgb(15, 15, 18);

            FftBits = (int)Math.Log(_fftSize, 2);
            complexBuffer = new Complex[_fftSize];

            pointsBuffer = Array.Empty<RawVector2>();
            blockyPointsBuffer = Array.Empty<RawVector2>();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!DesignMode) 
                InitDirect2D();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            // Properly check for ClientSize to avoid passing zero/invalid scales to Direct2D
            if (renderTarget != null && ClientSize.Width > 0 && ClientSize.Height > 0)
            {
                renderTarget.Resize(new Size2(ClientSize.Width, ClientSize.Height));
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if (e.Delta > 0)
                FftSize *= 2;
            else if (e.Delta < 0)
                FftSize /= 2;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // Always rely on ClientSize to ignore window borders
            if (ClientSize.Width > 0)
            {
                double normX = Math.Clamp((double)e.X / ClientSize.Width, 0.0, 1.0);
                double freq = MinFreq * Math.Pow(MaxFreq / MinFreq, normX);
                OnHoverFrequencyChanged(freq);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            OnHoverFrequencyChanged(null);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) CleanupDirect2D();
            base.Dispose(disposing);
        }

        private void InitDirect2D()
        {
            factory2D = new Factory2D();
            factoryDW = new FactoryDW();

            int initWidth = Math.Max(1, ClientSize.Width);
            int initHeight = Math.Max(1, ClientSize.Height);

            var properties = new HwndRenderTargetProperties
            {
                Hwnd = this.Handle,
                PixelSize = new Size2(initWidth, initHeight),
                PresentOptions = PresentOptions.Immediately
            };

            renderTarget = new WindowRenderTarget(
                factory2D,
                new RenderTargetProperties(new SharpDX.Direct2D1.PixelFormat(SharpDX.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied)),
                properties
            );

            renderTarget.TextAntialiasMode = TextAntialiasMode.Cleartype;
            renderTarget.AntialiasMode = AntialiasMode.PerPrimitive;

            fillBrush = new SolidColorBrush(renderTarget, new RawColor4(180f / 255f, 30f / 255f, 220f / 255f, 80f / 255f));
            lineBrush = new SolidColorBrush(renderTarget, new RawColor4(180f / 255f, 30f / 255f, 220f / 255f, 1f));
            gridBrush = new SolidColorBrush(renderTarget, new RawColor4(30f / 255f, 30f / 255f, 35f / 255f, 1f));
            textBrush = new SolidColorBrush(renderTarget, new RawColor4(100f / 255f, 100f / 255f, 110f / 255f, 1f));
            bandBrush = new SolidColorBrush(renderTarget, new RawColor4(50f / 255f, 50f / 255f, 60f / 255f, OverlayOpacity));
            statusBrush = new SolidColorBrush(renderTarget, new RawColor4(180f / 255f, 180f / 255f, 190f / 255f, 1f));
            blackTextBrush = new SolidColorBrush(renderTarget, new RawColor4(0f, 0f, 0f, OverlayOpacity));
            hoverLineBrush = new SolidColorBrush(renderTarget, new RawColor4(1f, 1f, 1f, 0.7f));
            hoverTextBrush = new SolidColorBrush(renderTarget, new RawColor4(1f, 1f, 1f, 1f));
            heatmapBrush = new SolidColorBrush(renderTarget, new RawColor4(0f, 0f, 0f, 1f));

            blackKeyBrush = new SolidColorBrush(renderTarget, new RawColor4(25f / 255f, 25f / 255f, 30f / 255f, OverlayOpacity));
            cKeyBrush = new SolidColorBrush(renderTarget, new RawColor4(110f / 255f, 20f / 255f, 40f / 255f, OverlayOpacity));
            cKeyTextBrush = new SolidColorBrush(renderTarget, new RawColor4(255f / 255f, 120f / 255f, 130f / 255f, OverlayOpacity));

            gridTextFormat = new TextFormat(factoryDW, "Consolas", 10f);
            statusTextFormat = new TextFormat(factoryDW, "Segoe UI", DWriteFontWeight.Normal, DWriteFontStyle.Normal, 12f)
            {
                TextAlignment = SharpDX.DirectWrite.TextAlignment.Center,
                ParagraphAlignment = ParagraphAlignment.Center
            };
            bandTextFormat = new TextFormat(factoryDW, "Consolas", DWriteFontWeight.Bold, DWriteFontStyle.Normal, 10f)
            {
                TextAlignment = SharpDX.DirectWrite.TextAlignment.Center,
                ParagraphAlignment = ParagraphAlignment.Center
            };
            hoverTextFormat = new TextFormat(factoryDW, "Consolas", DWriteFontWeight.Normal, DWriteFontStyle.Normal, 8.5f)
            {
                TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading,
                ParagraphAlignment = ParagraphAlignment.Far
            };
            keyTextFormat = new TextFormat(factoryDW, "Consolas", DWriteFontWeight.Normal, DWriteFontStyle.Normal, 8.5f)
            {
                TextAlignment = SharpDX.DirectWrite.TextAlignment.Center,
                ParagraphAlignment = ParagraphAlignment.Center
            };
        }

        private void CleanupDirect2D()
        {
            fillBrush?.Dispose();
            lineBrush?.Dispose();
            gridBrush?.Dispose();
            textBrush?.Dispose();
            bandBrush?.Dispose();
            statusBrush?.Dispose();
            blackTextBrush?.Dispose();
            heatmapBrush?.Dispose();
            hoverLineBrush?.Dispose();
            hoverTextBrush?.Dispose();

            blackKeyBrush?.Dispose();
            cKeyBrush?.Dispose();
            cKeyTextBrush?.Dispose();

            gridTextFormat?.Dispose();
            statusTextFormat?.Dispose();
            bandTextFormat?.Dispose();
            keyTextFormat?.Dispose();
            hoverTextFormat?.Dispose();

            renderTarget?.Dispose();
            factoryDW?.Dispose();
            factory2D?.Dispose();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (DesignMode) 
                base.OnPaintBackground(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (DesignMode)
            {
                e.Graphics.Clear(BackColor);
                e.Graphics.DrawString("Direct2D FFT Control (Designer Mode)", new System.Drawing.Font("Segoe UI", 10), System.Drawing.Brushes.White, 10, 10);
                return;
            }

            if (Bypass || renderTarget == null) 
                return;
            RenderD2D();
        }

        private void RenderD2D()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) 
                return;

            // Failsafe: Ensures mapping works even if WinForms swallowed the resize event during an anchoring pass 
            if (renderTarget.PixelSize.Width != ClientSize.Width || renderTarget.PixelSize.Height != ClientSize.Height)
            {
                renderTarget.Resize(new Size2(ClientSize.Width, ClientSize.Height));
            }

            renderTarget.BeginDraw();
            renderTarget.Clear(new RawColor4(15f / 255f, 15f / 255f, 18f / 255f, 1f));

            if (Engine != null && Engine.IsLoaded && !Engine.IsLoading)
            {
                ProcessFFT();
                DrawSpectrumHeatmap();
            }

            DrawGridD2D();
            DrawPianoKeysD2D();

            var fftSizeRect = new RawRectangleF(10, ClientSize.Height - 30, 200, ClientSize.Height - 20);
            renderTarget.DrawText($"{FftSize}", gridTextFormat, fftSizeRect, statusBrush);

            if (Engine != null && Engine.IsLoaded && !Engine.IsLoading)
            {
                DrawFFTGraph();
            }

            DrawPianoLabelsD2D();

            if (Engine != null && Engine.IsLoaded && ActiveHoverFrequency.HasValue && hoverLineBrush != null)
            {
                float hoverX = GetXForFrequency(ActiveHoverFrequency.Value, ClientSize.Width);
                if (hoverX >= 0 && hoverX <= ClientSize.Width)
                {
                    renderTarget.DrawLine(new RawVector2(hoverX, 0), new RawVector2(hoverX, ClientSize.Height), hoverLineBrush, 1.5f);

                    if (hoverTextFormat != null && hoverTextBrush != null)
                    {
                        string label = $"{AudioMath.GetNoteName(ActiveHoverFrequency.Value)} {ActiveHoverFrequency.Value:F1} Hz";
                        var textRect = new RawRectangleF(hoverX + 6, ClientSize.Height - 25, hoverX + 200, ClientSize.Height - 2);
                        renderTarget.DrawText(label, hoverTextFormat, textRect, hoverTextBrush);
                    }
                }
            }

            renderTarget.EndDraw();
        }

        private void DrawGridD2D()
        {
            float w = ClientSize.Width;
            float h = ClientSize.Height;

            foreach (var freq in ScaleFrequencies)
            {
                if (freq < MinFreq || freq > MaxFreq) 
                    continue;

                double normX = Math.Log(freq / MinFreq) / Math.Log(MaxFreq / MinFreq);
                float x = (float)(normX * w);

                renderTarget.DrawLine(new RawVector2(x, 0), new RawVector2(x, h), gridBrush, 1f);

                string label = freq >= 1000 ? $"{(freq / 1000)}k" : freq.ToString();
                var textRect = new RawRectangleF(x + 4, 0, x + 100, h);
                renderTarget.DrawText(label, gridTextFormat, textRect, textBrush);
            }

            var bands = new[]
            {
                ("PURR", Math.Min(20.0, MinFreq), 40.0),
                ("SUB", 40.0, 80.0),
                ("BASS", 80.0, 250.0),
                ("LOW MID", 250.0, 500.0),
                ("MID", 500.0, 2000.0),
                ("HIGH MID", 2000.0, 4000.0),
                ("PRS", 4000.0, 6000.0),
                ("TREBLE", 6000.0, Math.Max(20000.0, MaxFreq))
            };

            float bandY = 16f;
            float bandHeight = 16f;

            foreach (var band in bands)
            {
                double startF = Math.Max(MinFreq, band.Item2);
                double endF = Math.Min(MaxFreq, band.Item3);

                if (startF >= endF) 
                    continue;

                double normStartX = Math.Log(startF / MinFreq) / Math.Log(MaxFreq / MinFreq);
                double normEndX = Math.Log(endF / MinFreq) / Math.Log(MaxFreq / MinFreq);

                float startX = (float)(normStartX * w);
                float endX = (float)(normEndX * w);

                var rect = new RawRectangleF(startX + 1, bandY, endX - 1, bandY + bandHeight);
                renderTarget.FillRectangle(rect, bandBrush);
                renderTarget.DrawText(band.Item1, bandTextFormat, rect, blackTextBrush);
            }
        }

        private void DrawPianoKeysD2D()
        {
            if (bandBrush == null || blackKeyBrush == null || cKeyBrush == null) 
                return;

            float keyHeight = 12f;
            float pianoY = 32f;

            for (int n = 12; n <= 127; n++)
            {
                double freqBottom = 440.0 * Math.Pow(2.0, (n - 69 - 0.5) / 12.0);
                double freqTop = 440.0 * Math.Pow(2.0, (n - 69 + 0.5) / 12.0);

                if (freqTop < MinFreq || freqBottom > MaxFreq) 
                    continue;

                float xLeft = GetXForFrequency(freqBottom, ClientSize.Width);
                float xRight = GetXForFrequency(freqTop, ClientSize.Width);

                float keyWidth = Math.Max(1f, (xRight - xLeft) - 1f);
                bool isC = (n % 12 == 0);
                bool isBlack = (n % 12 == 1 || n % 12 == 3 || n % 12 == 6 || n % 12 == 8 || n % 12 == 10);

                SharpDX.Direct2D1.Brush b = isC ? cKeyBrush : (isBlack ? blackKeyBrush : bandBrush);
                var keyRect = new RawRectangleF(xLeft, pianoY, xLeft + keyWidth, pianoY + keyHeight);

                renderTarget.FillRectangle(keyRect, b);
            }
        }

        private void DrawPianoLabelsD2D()
        {
            if (keyTextFormat == null || cKeyTextBrush == null) 
                return;

            float keyHeight = 12f;
            float pianoY = 32f;

            for (int n = 12; n <= 127; n++)
            {
                if (n % 12 != 0) 
                    continue;

                double freqBottom = 440.0 * Math.Pow(2.0, (n - 69 - 0.5) / 12.0);
                double freqTop = 440.0 * Math.Pow(2.0, (n - 69 + 0.5) / 12.0);

                if (freqTop < MinFreq || freqBottom > MaxFreq) 
                    continue;

                float xLeft = GetXForFrequency(freqBottom, ClientSize.Width);
                float xRight = GetXForFrequency(freqTop, ClientSize.Width);
                float keyWidth = Math.Max(1f, xRight - xLeft);

                int octave = (n / 12) - 1;
                string cLabel = $"C{octave}";

                var cTextRect = new RawRectangleF(xLeft - 20, pianoY + keyHeight, xLeft + keyWidth + 20, pianoY + keyHeight + 14);
                renderTarget.DrawText(cLabel, keyTextFormat, cTextRect, cKeyTextBrush);
            }
        }

        private float GetXForFrequency(double freq, float clientWidth)
        {
            if (MinFreq >= MaxFreq) 
                return 0f;
            double normX = Math.Log(freq / MinFreq) / Math.Log(MaxFreq / MinFreq);
            return (float)(normX * clientWidth);
        }

        private void ProcessFFT()
        {
            var monoSamples = Engine.MonoSamples;
            if (monoSamples == null || monoSamples.Length == 0 || Engine.SampleRate <= 0) 
                return;

            long centerSample = (long)(Engine.Progress * monoSamples.Length);
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

            int width = ClientSize.Width;
            int height = ClientSize.Height;
            if (width <= 0 || height <= 0) 
                return;

            if (pointsBuffer == null || pointsBuffer.Length != width)
            {
                pointsBuffer = new RawVector2[width];
            }
            if (blockyPointsBuffer == null || blockyPointsBuffer.Length != width)
            {
                blockyPointsBuffer = new RawVector2[width];
            }

            double prevExactBin = (MinFreq * FftSize / Engine.SampleRate);

            for (int x = 0; x < width; x++)
            {
                double normX = width > 1 ? (double)x / (width - 1) : 0;
                double freq = MinFreq * Math.Pow(MaxFreq / MinFreq, normX);

                double exactBin = freq * FftSize / Engine.SampleRate;
                double maxMagSmooth = 0;
                double maxMagBlocky = 0;

                if (exactBin - prevExactBin < 1.0)
                {
                    int bin1 = (int)Math.Floor(exactBin);
                    int bin2 = bin1 + 1;
                    double fraction = exactBin - bin1;

                    bin1 = Math.Clamp(bin1, 1, (FftSize / 2) - 1);
                    bin2 = Math.Clamp(bin2, 1, (FftSize / 2) - 1);

                    double mag1 = Math.Sqrt(complexBuffer[bin1].X * complexBuffer[bin1].X + complexBuffer[bin1].Y * complexBuffer[bin1].Y);
                    double mag2 = Math.Sqrt(complexBuffer[bin2].X * complexBuffer[bin2].X + complexBuffer[bin2].Y * complexBuffer[bin2].Y);

                    maxMagSmooth = mag1 + (mag2 - mag1) * fraction;
                    maxMagBlocky = mag1;
                }
                else
                {
                    int startBin = (int)Math.Ceiling(prevExactBin);
                    int endBin = (int)Math.Floor(exactBin);

                    startBin = Math.Clamp(startBin, 1, (FftSize / 2) - 1);
                    endBin = Math.Clamp(endBin, 1, (FftSize / 2) - 1);
                    if (startBin > endBin) startBin = endBin;

                    double maxMag = 0;
                    for (int b = startBin; b <= endBin; b++)
                    {
                        double real = complexBuffer[b].X;
                        double imag = complexBuffer[b].Y;
                        double mag = Math.Sqrt(real * real + imag * imag);
                        if (mag > maxMag) maxMag = mag;
                    }

                    maxMagSmooth = maxMag;
                    maxMagBlocky = maxMag;
                }

                prevExactBin = exactBin;

                double dbSmooth = 20.0 * Math.Log10(Math.Max(maxMagSmooth, 1e-6));
                float normYSmooth = Math.Clamp((float)((dbSmooth - MinDb) / (MaxDb - MinDb)), 0f, 1f);
                float ySmooth = height - (normYSmooth * height);
                pointsBuffer[x] = new RawVector2(x, ySmooth);

                double dbBlocky = 20.0 * Math.Log10(Math.Max(maxMagBlocky, 1e-6));
                float normYBlocky = Math.Clamp((float)((dbBlocky - MinDb) / (MaxDb - MinDb)), 0f, 1f);
                float yBlocky = height - (normYBlocky * height);
                blockyPointsBuffer[x] = new RawVector2(x, yBlocky);
            }
        }

        private void DrawSpectrumHeatmap()
        {
            int width = ClientSize.Width;
            int height = ClientSize.Height;

            if (pointsBuffer == null || pointsBuffer.Length != width || heatmapBrush == null) 
                return;

            for (int x = 0; x < width; x++)
            {
                float y = pointsBuffer[x].Y;
                float normY = Math.Clamp((height - y) / height, 0f, 1f);

                heatmapBrush.Color = SpectralColorMapper.GetSpectralColorRaw4(normY, 1.0f);
                renderTarget.DrawLine(
                    new RawVector2(x, 0),
                    new RawVector2(x, height),
                    heatmapBrush,
                    1.0f
                );
            }
        }

        private void DrawFFTGraph()
        {
            int width = ClientSize.Width;
            int height = ClientSize.Height;

            var targetBuffer = SmoothForeground ? pointsBuffer : blockyPointsBuffer;
            if (targetBuffer == null || targetBuffer.Length != width || width <= 1) return;

            using var fillGeom = new PathGeometry(factory2D);
            using (var sink = fillGeom.Open())
            {
                sink.BeginFigure(new RawVector2(0, height), FigureBegin.Filled);
                sink.AddLine(targetBuffer[0]);

                for (int i = 1; i < width; i++)
                {
                    sink.AddLine(targetBuffer[i]);
                }

                sink.AddLine(new RawVector2(width - 1, height));
                sink.EndFigure(FigureEnd.Closed);
                sink.Close();
            }
            renderTarget.FillGeometry(fillGeom, fillBrush);

            using var lineGeom = new PathGeometry(factory2D);
            using (var sink = lineGeom.Open())
            {
                sink.BeginFigure(targetBuffer[0], FigureBegin.Hollow);

                for (int i = 1; i < width; i++)
                {
                    sink.AddLine(targetBuffer[i]);
                }

                sink.EndFigure(FigureEnd.Open);
                sink.Close();
            }
            renderTarget.DrawGeometry(lineGeom, lineBrush, 2f);
        }
    }
}