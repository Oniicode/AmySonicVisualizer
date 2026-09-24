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

        private int _fftSize = 8192;
        public int FftBits { get; private set; } = 12;

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

                    // Reallocate the complex buffer to accommodate the new window size
                    complexBuffer = new Complex[_fftSize];

                    FftSizeChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate(); // Trigger a redraw immediately using the new resolution
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

        private TextFormat gridTextFormat;
        private TextFormat statusTextFormat;
        private TextFormat bandTextFormat;

        private Complex[] complexBuffer;
        private RawVector2[] pointsBuffer;

        public SpectrumVisualizerControl()
        {
            // Optimize WinForms control flags for custom hardware rendering
            SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);

            // FIX: Disable GDI+ double buffering inherited from VisualizerControlBase
            // so WinForms stops painting an empty background over the Direct2D render
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
            DoubleBuffered = false;

            BackColor = System.Drawing.Color.FromArgb(15, 15, 18);
            complexBuffer = new Complex[FftSize];
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!DesignMode) InitDirect2D();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (renderTarget != null)
            {
                renderTarget.Resize(new Size2(Width, Height));
                pointsBuffer = new RawVector2[Width];
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

        protected override void Dispose(bool disposing)
        {
            if (disposing) CleanupDirect2D();
            base.Dispose(disposing);
        }

        private void InitDirect2D()
        {
            factory2D = new Factory2D();
            factoryDW = new FactoryDW();

            var properties = new HwndRenderTargetProperties
            {
                Hwnd = this.Handle,
                PixelSize = new Size2(Width, Height),
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
            bandBrush = new SolidColorBrush(renderTarget, new RawColor4(50f / 255f, 50f / 255f, 60f / 255f, 1f));
            statusBrush = new SolidColorBrush(renderTarget, new RawColor4(180f / 255f, 180f / 255f, 190f / 255f, 1f));
            blackTextBrush = new SolidColorBrush(renderTarget, new RawColor4(0f, 0f, 0f, 1f));

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

            pointsBuffer = new RawVector2[Width];
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

            gridTextFormat?.Dispose();
            statusTextFormat?.Dispose();
            bandTextFormat?.Dispose();

            renderTarget?.Dispose();
            factoryDW?.Dispose();
            factory2D?.Dispose();
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
                e.Graphics.DrawString("Direct2D FFT Control (Designer Mode)", new System.Drawing.Font("Segoe UI", 10), System.Drawing.Brushes.White, 10, 10);
                return;
            }

            if (Bypass || renderTarget == null) return;
            RenderD2D();
        }

        private void RenderD2D()
        {
            renderTarget.BeginDraw();
            renderTarget.Clear(new RawColor4(15f / 255f, 15f / 255f, 18f / 255f, 1f));

            DrawGridD2D();

            // Display current FFT window size as an on-screen overlay to provide visual feedback for mouse-wheel scaling
            var fftSizeRect = new RawRectangleF(10, 10, 200, 30);
            renderTarget.DrawText($"FFT Window Size: {FftSize}", gridTextFormat, fftSizeRect, statusBrush);

            if (Engine == null || !Engine.IsLoaded)
            {
                renderTarget.EndDraw();
                return;
            }

            ProcessAndDrawFFT();
            renderTarget.EndDraw();
        }

        private void DrawGridD2D()
        {
            float w = Width;
            float h = Height;

            // Draw primary scale frequencies
            foreach (var freq in ScaleFrequencies)
            {
                if (freq < MinFreq || freq > MaxFreq) continue;

                double normX = Math.Log(freq / MinFreq) / Math.Log(MaxFreq / MinFreq);
                float x = (float)(normX * w);

                renderTarget.DrawLine(new RawVector2(x, 0), new RawVector2(x, h), gridBrush, 1f);

                string label = freq >= 1000 ? $"{(freq / 1000)}k" : freq.ToString();
                var textRect = new RawRectangleF(x + 4, 0, x + 100, h);
                renderTarget.DrawText(label, gridTextFormat, textRect, textBrush);
            }

            // Define and draw EQ bands below the scale frequencies
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

                if (startF >= endF) continue;

                double normStartX = Math.Log(startF / MinFreq) / Math.Log(MaxFreq / MinFreq);
                double normEndX = Math.Log(endF / MinFreq) / Math.Log(MaxFreq / MinFreq);

                float startX = (float)(normStartX * w);
                float endX = (float)(normEndX * w);

                // Add a small 1px padding left and right to separate the boxes visually
                var rect = new RawRectangleF(startX + 1, bandY, endX - 1, bandY + bandHeight);

                // Draw grey background box
                renderTarget.FillRectangle(rect, bandBrush);

                // Draw black, centered text inside
                renderTarget.DrawText(band.Item1, bandTextFormat, rect, blackTextBrush);
            }
        }

        private void ProcessAndDrawFFT()
        {
            var monoSamples = Engine.MonoSamples;
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

            int width = Width;
            int height = Height;

            if (pointsBuffer == null || pointsBuffer.Length != width)
            {
                pointsBuffer = new RawVector2[width];
            }

            int prevBin = 1;

            // Map purely by screen X to log-space to ensure even octave spacing.
            // A bin range search guarantees no peaks are dropped in the high frequencies.
            for (int x = 0; x < width; x++)
            {
                double normX = (double)x / (width - 1);
                double freq = MinFreq * Math.Pow(MaxFreq / MinFreq, normX);

                int bin = (int)Math.Round(freq * FftSize / Engine.SampleRate);
                bin = Math.Clamp(bin, 1, (FftSize / 2) - 1);

                double maxMag = 0;
                int startBin = (x == 0) ? bin : prevBin + 1;
                if (startBin > bin) startBin = bin;

                for (int b = startBin; b <= bin; b++)
                {
                    double real = complexBuffer[b].X;
                    double imag = complexBuffer[b].Y;
                    double mag = Math.Sqrt(real * real + imag * imag);
                    if (mag > maxMag) maxMag = mag;
                }

                prevBin = bin;

                double db = 20.0 * Math.Log10(Math.Max(maxMag, 1e-6));
                float normY = Math.Clamp((float)((db - MinDb) / (MaxDb - MinDb)), 0f, 1f);
                float y = height - (normY * height);

                pointsBuffer[x] = new RawVector2(x, y);
            }

            if (width > 1)
            {
                using var fillGeom = new PathGeometry(factory2D);
                using (var sink = fillGeom.Open())
                {
                    sink.BeginFigure(new RawVector2(0, height), FigureBegin.Filled);
                    sink.AddLine(pointsBuffer[0]);

                    for (int i = 1; i < width; i++)
                    {
                        sink.AddLine(pointsBuffer[i]);
                    }

                    sink.AddLine(new RawVector2(width - 1, height));
                    sink.EndFigure(FigureEnd.Closed);
                    sink.Close();
                }
                renderTarget.FillGeometry(fillGeom, fillBrush);

                using var lineGeom = new PathGeometry(factory2D);
                using (var sink = lineGeom.Open())
                {
                    sink.BeginFigure(pointsBuffer[0], FigureBegin.Hollow);

                    for (int i = 1; i < width; i++)
                    {
                        sink.AddLine(pointsBuffer[i]);
                    }

                    sink.EndFigure(FigureEnd.Open);
                    sink.Close();
                }
                renderTarget.DrawGeometry(lineGeom, lineBrush, 2f);
            }
        }
    }
}