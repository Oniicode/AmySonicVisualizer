#nullable enable
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
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
    /// <summary>
    /// The algorithm used to generate the spectrogram data.
    /// </summary>
    public enum SpectrogramAlgorithmType
    {
        FFT,
        CQT
    }

    public delegate void SpectrogramProgressCallback(int startX, int endX, double[,] dbCache);

    /// <summary>
    /// Interface for modularizing graphical spectrogram generation methods.
    /// </summary>
    public interface ISpectrogramAnalyzer
    {
        /// <summary>
        /// Analyzes the given audio samples and generates a decibel (dB) cache mapping to X (width) and Y (height).
        /// Takes an optional callback to yield calculated chunks incrementally and a cancellation token to abort.
        /// </summary>
        double[,] Analyze(float[] monoSamples, int sampleRate, int width, int height, double minFreq, double maxFreq, SpectrogramProgressCallback? progressCallback = null, CancellationToken cancellationToken = default);

        public string ModeDisplay { get; }
    }

    /// <summary>
    /// Generates a spectrogram using the Fast Fourier Transform (FFT).
    /// </summary>
    public class FftSpectrogramAnalyzer : ISpectrogramAnalyzer
    {
        public const int MaxFftSize = 32768 * 2 * 2;
        public const int DefaultFftSize = 32768 / 2;

        public int FftSize { get; set; } = DefaultFftSize;

        public string ModeDisplay => $"FFT ({FftSize}-window)";

        public double[,] Analyze(float[] monoSamples, int sampleRate, int width, int height, double minFreq, double maxFreq, SpectrogramProgressCallback? progressCallback = null, CancellationToken cancellationToken = default)
        {
            int fftBits = (int)Math.Round(Math.Log(FftSize, 2));
            double[,] dbCache = new double[width, height];

            // Pre-calculate which FFT bin corresponds to which Y pixel on the screen
            int[] rowToBin = new int[height];
            for (int y = 0; y < height; y++)
            {
                double normY = (double)(height - 1 - y) / height;
                double freq = minFreq * Math.Pow(maxFreq / minFreq, normY);
                int bin = (int)Math.Round(freq * FftSize / sampleRate);
                rowToBin[y] = Math.Clamp(bin, 0, (FftSize / 2) - 1);
            }

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
            };

            // Break the width down into ~64 incremental chunks for progressive UI rendering
            int chunkSize = Math.Max(1, width / 64);

            for (int chunkStart = 0; chunkStart < width; chunkStart += chunkSize)
            {
                if (cancellationToken.IsCancellationRequested) break;
                int chunkEnd = Math.Min(width, chunkStart + chunkSize);

                // Process time slices (X-axis) within the chunk in parallel for a massive performance boost
                Parallel.For(chunkStart, chunkEnd, parallelOptions, (x, state) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        state.Stop();
                        return;
                    }

                    var complexBuffer = new NAudio.Dsp.Complex[FftSize];
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

                    FastFourierTransform.FFT(true, fftBits, complexBuffer);

                    for (int y = 0; y < height; y++)
                    {
                        int bin = rowToBin[y];
                        double real = complexBuffer[bin].X;
                        double imag = complexBuffer[bin].Y;
                        double mag = Math.Sqrt(real * real + imag * imag);
                        dbCache[x, y] = 20.0 * Math.Log10(Math.Max(mag, 1e-6));
                    }
                });

                if (cancellationToken.IsCancellationRequested) break;

                progressCallback?.Invoke(chunkStart, chunkEnd, dbCache);
            }

            return dbCache;
        }
    }

    /// <summary>
    /// Generates a spectrogram using the Constant-Q Transform (CQT) via a time-domain exact filterbank.
    /// Highly optimized using pre-calculated complex window kernels, SIMD vectorization, and branchless bounds.
    /// </summary>
    public class CqtSpectrogramAnalyzer : ISpectrogramAnalyzer
    {
        public const int DefaultCqtBinsPerOctave = 12 * 10;
        public const int MaxCqtBinsPerOctave = 120;

        public int BinsPerOctave { get; set; } = DefaultCqtBinsPerOctave;
        public int MaxCqtWindowSize { get; set; } = 16384;

        // Multiplier to somewhat align CQT output decibels with FFT decibel ranges
        public double CqtGainMultiplier { get; set; } = 25.0;

        public string ModeDisplay => $"CQT ({BinsPerOctave} bins/octave)";

        public double[,] Analyze(float[] monoSamples, int sampleRate, int width, int height, double minFreq, double maxFreq, SpectrogramProgressCallback? progressCallback = null, CancellationToken cancellationToken = default)
        {
            double[,] dbCache = new double[width, height];

            // Q factor derivation: Q = f / delta_f
            double Q = 1.0 / (Math.Pow(2, 1.0 / BinsPerOctave) - 1.0);

            float[][] kernelReal = new float[height][];
            float[][] kernelImag = new float[height][];

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
            };

            // Kernel pre-calculation is executed in Parallel
            Parallel.For(0, height, parallelOptions, (y, state) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    state.Stop();
                    return;
                }

                double normY = (double)(height - 1 - y) / height;
                double freq = minFreq * Math.Pow(maxFreq / minFreq, normY);

                // Window length N is inversely proportional to frequency
                int N = (int)Math.Round(sampleRate * Q / freq);
                N = Math.Clamp(N, 16, MaxCqtWindowSize);

                kernelReal[y] = new float[N];
                kernelImag[y] = new float[N];

                double phaseStep = 2.0 * Math.PI * freq / sampleRate;

                for (int i = 0; i < N; i++)
                {
                    // Hann Window
                    double window = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (N - 1)));
                    double phase = i * phaseStep;

                    // Complex conjugate exponential
                    kernelReal[y][i] = (float)(window * Math.Cos(phase));
                    kernelImag[y][i] = (float)(-window * Math.Sin(phase));
                }
            });

            if (cancellationToken.IsCancellationRequested)
                return dbCache;

            // Break the width down into ~64 incremental chunks for progressive UI rendering
            int chunkSize = Math.Max(1, width / 64);

            for (int chunkStart = 0; chunkStart < width; chunkStart += chunkSize)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                int chunkEnd = Math.Min(width, chunkStart + chunkSize);

                Parallel.For(chunkStart, chunkEnd, parallelOptions, (x, state) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        state.Stop();
                        return;
                    }

                    long centerSample = (long)x * monoSamples.Length / width;

                    for (int y = 0; y < height; y++)
                    {
                        var kReal = kernelReal[y];
                        var kImag = kernelImag[y];
                        int N = kReal.Length;
                        int startSample = (int)(centerSample - (N / 2));

                        // Branchless inner loops by calculating the exact safe array boundaries
                        int startI = Math.Max(0, -startSample);
                        int endI = (int)Math.Min(N, (long)monoSamples.Length - startSample);

                        float real = 0f;
                        float imag = 0f;

                        if (endI > startI)
                        {
                            int i = startI;

                            // Hardware-accelerated SIMD Multiply-Accumulate
                            if (Vector.IsHardwareAccelerated)
                            {
                                int vectorSize = Vector<float>.Count;
                                int vectorEnd = startI + ((endI - startI) / vectorSize) * vectorSize;

                                Vector<float> vRealSum = Vector<float>.Zero;
                                Vector<float> vImagSum = Vector<float>.Zero;

                                for (; i < vectorEnd; i += vectorSize)
                                {
                                    var vSamples = new Vector<float>(monoSamples, startSample + i);
                                    var vKReal = new Vector<float>(kReal, i);
                                    var vKImag = new Vector<float>(kImag, i);

                                    vRealSum += vSamples * vKReal;
                                    vImagSum += vSamples * vKImag;
                                }

                                // Efficient collapse of vector sums
                                real += Vector.Dot(vRealSum, Vector<float>.One);
                                imag += Vector.Dot(vImagSum, Vector<float>.One);
                            }

                            // Remainder loop
                            for (; i < endI; i++)
                            {
                                float sampleVal = monoSamples[startSample + i];
                                real += sampleVal * kReal[i];
                                imag += sampleVal * kImag[i];
                            }
                        }

                        // Normalize magnitude by window size (N) to keep levels consistent across frequencies
                        double mag = Math.Sqrt(real * real + imag * imag) / N;
                        dbCache[x, y] = 20.0 * Math.Log10(Math.Max(mag * CqtGainMultiplier, 1e-6));
                    }
                });

                if (cancellationToken.IsCancellationRequested)
                    break;

                progressCallback?.Invoke(chunkStart, chunkEnd, dbCache);
            }

            return dbCache;
        }
    }

    public class SpectrogramVisualizerControl : BaseVisualizerControl
    {
        private SpectrogramAlgorithmType _analysisMethod = SpectrogramAlgorithmType.CQT;

        [Category("Spectrogram Settings")]
        [Description("The algorithm used to compute the spectrogram.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        [DefaultValue(SpectrogramAlgorithmType.CQT)]
        public SpectrogramAlgorithmType AnalysisMethod
        {
            get => _analysisMethod;
            set
            {
                if (_analysisMethod != value)
                {
                    _analysisMethod = value;
                    TriggerReanalysis();
                }
            }
        }

        private int _fftSize = FftSpectrogramAnalyzer.DefaultFftSize;

        [Category("Spectrogram Settings")]
        [Description("The size of the FFT window. Internally snaps to the nearest power of 2. (Used when Method is FFT)")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        [DefaultValue(FftSpectrogramAnalyzer.DefaultFftSize)]
        public int FftSize
        {
            get => _fftSize;
            set
            {
                int clamped = Math.Clamp(value, 256, FftSpectrogramAnalyzer.MaxFftSize);
                int validFftSize = (int)Math.Pow(2, Math.Round(Math.Log(clamped, 2)));

                if (_fftSize != validFftSize)
                {
                    _fftSize = validFftSize;
                    if (_analysisMethod == SpectrogramAlgorithmType.FFT)
                        TriggerReanalysis();
                }
            }
        }

        private int _cqtBinsPerOctave = CqtSpectrogramAnalyzer.DefaultCqtBinsPerOctave;

        [Category("Spectrogram Settings")]
        [Description("Determines frequency resolution for Constant-Q Transform. (Used when Method is CQT)")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        [DefaultValue(CqtSpectrogramAnalyzer.DefaultCqtBinsPerOctave)]
        public int CqtBinsPerOctave
        {
            get => _cqtBinsPerOctave;
            set
            {
                int clamped = Math.Clamp(value, 12, CqtSpectrogramAnalyzer.MaxCqtBinsPerOctave);
                if (_cqtBinsPerOctave != clamped)
                {
                    _cqtBinsPerOctave = clamped;
                    if (_analysisMethod == SpectrogramAlgorithmType.CQT)
                        TriggerReanalysis();
                }
            }
        }

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

        private bool _smoothSpectrogram = true;

        [Category("Spectrogram Settings")]
        [Description("Toggles whether the spectrogram rendering is smoothed (linear) or pixelated (nearest neighbor).")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        [DefaultValue(true)]
        public bool SmoothSpectrogram
        {
            get => _smoothSpectrogram;
            set
            {
                if (_smoothSpectrogram != value)
                {
                    _smoothSpectrogram = value;
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double[] ScaleFrequencies { get; set; } = [50, 100, 200, 500, 1000, 2000, 5000, 10000];

        private bool _isProcessing = false;
        private string _statusMessage = "Press [Ctrl+O] or Click Here to Load Audio Data";

        private double[,]? _dbCache;
        private int _cachedWidth;
        private int _cachedHeight;
        private int[]? _pixelBuffer;

        // Manage active analysis lifecycle and allow safe cancellation
        private CancellationTokenSource? _analysisCts;

        // Tracking how many columns of the new analysis have been completed
        private volatile int _analysisProgressX = 0;

        private bool _revealFuture = false;

        public event EventHandler? RevealFutureChanged;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool RevealFuture
        {
            get => _revealFuture;
            set
            {
                if (_revealFuture != value)
                {
                    _revealFuture = value;
                    RevealFutureChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        private double _gainOffset = 0.0;

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

        // Active GPU bitmap for the incoming/completed analysis
        private D2DBitmap? _d2dSpectrogramBitmap;
        // Background GPU bitmap layer that persists through processing a new analysis
        private D2DBitmap? _backgroundBitmap;

        // Brushes
        private SolidColorBrush? _unrevealedBrush;
        private SolidColorBrush? _cursorLineBrush;
        private SolidColorBrush? _hoverLineBrush;
        private SolidColorBrush? _hoverTextBrush;
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
        private SolidColorBrush? _statusOverlayBrush;

        // Text Formats
        private TextFormat? _statusTextFormat;
        private TextFormat? _scaleTextFormat;
        private TextFormat? _gainTextFormat;
        private TextFormat? _hoverTextFormat;

        public SpectrogramVisualizerControl()
        {
            SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
            DoubleBuffered = false;

            BackColor = System.Drawing.Color.FromArgb(15, 15, 18);
            MouseDown += OnMouseDown;
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
            _hoverLineBrush = new SolidColorBrush(_renderTarget, new RawColor4(1f, 1f, 1f, 0.7f));
            _hoverTextBrush = new SolidColorBrush(_renderTarget, new RawColor4(1f, 1f, 1f, 1f));

            _scaleBgBrush = new SolidColorBrush(_renderTarget, new RawColor4(10f / 255f, 10f / 255f, 14f / 255f, 180f / 255f));
            _whiteKeyBrush = new SolidColorBrush(_renderTarget, new RawColor4(220f / 255f, 220f / 255f, 225f / 255f, 1f));

            const float PianoOverlayOpacity = 0.25f;
            _blackKeyBrush = new SolidColorBrush(_renderTarget, new RawColor4(25f / 255f, 25f / 255f, 30f / 255f, PianoOverlayOpacity));
            _cKeyBrush = new SolidColorBrush(_renderTarget, new RawColor4(110f / 255f, 20f / 255f, 40f / 255f, PianoOverlayOpacity));
            _cKeyTextBrush = new SolidColorBrush(_renderTarget, new RawColor4(255f / 255f, 120f / 255f, 130f / 255f, 1f));
            _keyBorderBrush = new SolidColorBrush(_renderTarget, new RawColor4(10f / 255f, 10f / 255f, 14f / 255f, 1f));

            _tickBrush = new SolidColorBrush(_renderTarget, new RawColor4(50f / 255f, 50f / 255f, 60f / 255f, 1f));
            _textBrush = new SolidColorBrush(_renderTarget, new RawColor4(100f / 255f, 100f / 255f, 110f / 255f, 1f));
            _statusBrush = new SolidColorBrush(_renderTarget, new RawColor4(255f / 255f, 255f / 255f, 255f / 255f, 1f));
            _gainBrush = new SolidColorBrush(_renderTarget, new RawColor4(235f / 255f, 235f / 255f, 245f / 255f, 1f));

            // Dim overlay shown when processing new spectrograms
            _statusOverlayBrush = new SolidColorBrush(_renderTarget, new RawColor4(0f, 0f, 0f, 0.65f));

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

            _hoverTextFormat = new TextFormat(_factoryDW, "Consolas", DWriteFontWeight.Normal, DWriteFontStyle.Normal, 8.5f)
            {
                TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading,
                ParagraphAlignment = ParagraphAlignment.Far
            };
        }

        private void CleanupDirect2D()
        {
            _backgroundBitmap?.Dispose();
            _d2dSpectrogramBitmap?.Dispose();
            _unrevealedBrush?.Dispose();
            _cursorLineBrush?.Dispose();
            _hoverLineBrush?.Dispose();
            _hoverTextBrush?.Dispose();
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
            _statusOverlayBrush?.Dispose();

            _statusTextFormat?.Dispose();
            _scaleTextFormat?.Dispose();
            _gainTextFormat?.Dispose();
            _hoverTextFormat?.Dispose();

            _renderTarget?.Dispose();
            _factoryDW?.Dispose();
            _factory2D?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _analysisCts?.Cancel();
                _analysisCts?.Dispose();
                CleanupDirect2D();
            }
            base.Dispose(disposing);
        }

        public override void BindEngine(AudioEngine engine)
        {
            base.BindEngine(engine);

            engine.AudioLoading += (s, e) =>
            {
                // Ensure any in-flight visualizer generation task halts entirely 
                _analysisCts?.Cancel();

                _isProcessing = true;
                _statusMessage = "Loading ...";

                _dbCache = null;
                _backgroundBitmap?.Dispose();
                _backgroundBitmap = null;
                _d2dSpectrogramBitmap?.Dispose();
                _d2dSpectrogramBitmap = null;

                RevealFuture = false;
            };

            engine.FileLoaded += async (s, e) =>
            {
                await RegenerateSpectrogramAsync();
            };
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_isProcessing || _dbCache == null)
                return;
            double gainChange = (e.Delta / 120.0) * 2.5;
            GainOffset += gainChange;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (Height > 0)
            {
                double normY = Math.Clamp((Height - 1 - e.Y) / (double)Height, 0.0, 1.0);
                double freq = MinFreq * Math.Pow(MaxFreq / MinFreq, normY);
                OnHoverFrequencyChanged(freq);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            OnHoverFrequencyChanged(null);
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (Engine == null)
                return;

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

        private void TriggerReanalysis()
        {
            if (Engine != null && Engine.IsLoaded)
            {
                _ = RegenerateSpectrogramAsync();
            }
        }

        private ISpectrogramAnalyzer GetActiveAnalyzer()
        {
            return _analysisMethod switch
            {
                SpectrogramAlgorithmType.CQT => new CqtSpectrogramAnalyzer { BinsPerOctave = _cqtBinsPerOctave },
                SpectrogramAlgorithmType.FFT => new FftSpectrogramAnalyzer { FftSize = _fftSize },
                _ => new FftSpectrogramAnalyzer { FftSize = _fftSize }
            };
        }

        private async Task RegenerateSpectrogramAsync()
        {
            if (Engine == null)
                return;

            // Cancel any ongoing process before starting a new one
            _analysisCts?.Cancel();
            _analysisCts?.Dispose();
            _analysisCts = new CancellationTokenSource();
            var token = _analysisCts.Token;

            _isProcessing = true;

            int width = Math.Max(Screen.PrimaryScreen?.WorkingArea.Width ?? Width, 1);
            int height = Math.Max(Screen.PrimaryScreen?.WorkingArea.Height ?? Height, 1);

            float[] monoSamples = Engine.MonoSamples;
            int sampleRate = Engine.SampleRate;

            var analyzer = GetActiveAnalyzer();

            _statusMessage = $"Analyzing {analyzer.ModeDisplay} ...";

            _cachedWidth = width;
            _cachedHeight = height;

            // Shift the current active bitmap to become the background rendering canvas
            if (_d2dSpectrogramBitmap != null)
            {
                _backgroundBitmap?.Dispose();
                _backgroundBitmap = _d2dSpectrogramBitmap;
                _d2dSpectrogramBitmap = null;
            }

            _pixelBuffer = new int[width * height];
            _analysisProgressX = 0;
            _gainOffset = 0.0;

            // Clear out references so the UI synchronization thread can validate incoming callbacks
            _dbCache = null;

            Invalidate();

            try
            {
                // UI Synchronized chunk-callback ensuring thread safety and progress tracking
                void OnProgress(int startX, int endX, double[,] partialCache)
                {
                    if (this.IsDisposed || token.IsCancellationRequested)
                        return;

                    BeginInvoke(new Action(() =>
                    {
                        // Ensure we discard stale callbacks if another process spawned immediately after
                        if (this.IsDisposed || token.IsCancellationRequested)
                            return;
                        if (_dbCache != null && _dbCache != partialCache)
                            return;

                        _dbCache ??= partialCache;
                        UpdateBitmapChunk(startX, endX);
                        _analysisProgressX = endX;
                        Invalidate();
                    }));
                }

                // Offload the entire blockwise generation to a background task
                var computedCache = await Task.Run(() => analyzer.Analyze(monoSamples, sampleRate, width, height, MinFreq, MaxFreq, OnProgress, token), token);

                // Safe fallback to resolve any late-stage mismatches
                if (!token.IsCancellationRequested)
                {
                    _dbCache = computedCache;
                    _analysisProgressX = width;
                    UpdateBitmapChunk(0, width);
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    MessageBox.Show($"Failed to generate spectrogram: {ex.Message}");
                    _statusMessage = "Press [O] or Click to Open Audio File";
                }
            }
            finally
            {
                // Only unset processing state if this is still the authoritative task
                if (!token.IsCancellationRequested)
                {
                    _isProcessing = false;
                    Invalidate();
                }
            }
        }

        private void UpdateBitmapChunk(int startX, int endX)
        {
            if (_dbCache == null || _renderTarget == null || DesignMode)
                return;
            if (_pixelBuffer == null || _pixelBuffer.Length != _cachedWidth * _cachedHeight)
                return;

            double currentMinDb = MinDb - _gainOffset;
            double currentMaxDb = MaxDb - _gainOffset;

            // Map purely the computed chunk to spectral pixel colors
            for (int y = 0; y < _cachedHeight; y++)
            {
                int yOffset = y * _cachedWidth;
                for (int x = startX; x < endX; x++)
                {
                    double db = _dbCache[x, y];
                    float norm = Math.Clamp((float)((db - currentMinDb) / (currentMaxDb - currentMinDb)), 0f, 1f);
                    _pixelBuffer[yOffset + x] = SpectralColorMapper.GetSpectralColorInt(norm);
                }
            }

            if (_d2dSpectrogramBitmap == null || _d2dSpectrogramBitmap.PixelSize.Width != _cachedWidth || _d2dSpectrogramBitmap.PixelSize.Height != _cachedHeight)
            {
                _d2dSpectrogramBitmap?.Dispose();
                var bmpProps = new BitmapProperties(new PixelFormat(SharpDX.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied));
                _d2dSpectrogramBitmap = new D2DBitmap(_renderTarget, new Size2(_cachedWidth, _cachedHeight), bmpProps);
            }

            var handle = GCHandle.Alloc(_pixelBuffer, GCHandleType.Pinned);
            try
            {
                _d2dSpectrogramBitmap.CopyFromMemory(handle.AddrOfPinnedObject(), _cachedWidth * 4);
            }
            finally
            {
                handle.Free();
            }
        }

        private void ReapplyColorsD2D()
        {
            // Suppress global recalculations if a progressive render is currently happening
            if (_isProcessing)
                return;
            UpdateBitmapChunk(0, _cachedWidth);
            Invalidate();
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
                e.Graphics.DrawString("Direct2D Spectrogram Control (Designer Mode)", new System.Drawing.Font("Segoe UI", 10), System.Drawing.Brushes.White, 10, 10);
                return;
            }

            if (Bypass || _renderTarget == null)
                return;
            RenderD2D();
        }

        private void RenderD2D()
        {
            if (_renderTarget == null)
                return;

            _renderTarget.BeginDraw();
            _renderTarget.Clear(new RawColor4(15f / 255f, 15f / 255f, 18f / 255f, 1f));

            if (Engine == null || !Engine.IsLoaded || (_d2dSpectrogramBitmap == null && _backgroundBitmap == null))
            {
                if (_statusTextFormat != null && _statusBrush != null)
                {
                    _renderTarget.DrawText(_statusMessage, _statusTextFormat, new RawRectangleF(0, 0, Width, Height), _statusBrush);
                }
                _renderTarget.EndDraw();
                return;
            }

            // X-Coordinate bounding conditions
            float playbackX = _revealFuture ? Width : (float)(Engine.Progress * Width);
            float analysisX = _isProcessing ? ((float)_analysisProgressX / _cachedWidth) * Width : Width;

            BitmapInterpolationMode interpolationMode = _smoothSpectrogram
                ? BitmapInterpolationMode.Linear
                : BitmapInterpolationMode.NearestNeighbor;

            // 1. Draw the newly processed foreground bitmap natively filling left-to-right up to playbackX constraint
            float newDrawEndX = Math.Min(analysisX, playbackX);
            if (newDrawEndX > 0 && _d2dSpectrogramBitmap != null)
            {
                float srcEndX = (newDrawEndX / Width) * _d2dSpectrogramBitmap.PixelSize.Width;
                var destRect = new RawRectangleF(0, 0, newDrawEndX, Height);
                var srcRect = new RawRectangleF(0, 0, srcEndX, _d2dSpectrogramBitmap.PixelSize.Height);
                _renderTarget.DrawBitmap(_d2dSpectrogramBitmap, destRect, 1.0f, interpolationMode, srcRect);
            }

            // 2. Draw the background bitmap filling the remainder (acting as layered canvas)
            if (playbackX > analysisX && _backgroundBitmap != null)
            {
                float oldDrawStartX = analysisX;
                float oldDrawEndX = playbackX;
                float srcStartX = (oldDrawStartX / Width) * _backgroundBitmap.PixelSize.Width;
                float srcEndX = (oldDrawEndX / Width) * _backgroundBitmap.PixelSize.Width;

                var destRect = new RawRectangleF(oldDrawStartX, 0, oldDrawEndX, Height);
                var srcRect = new RawRectangleF(srcStartX, 0, srcEndX, _backgroundBitmap.PixelSize.Height);
                _renderTarget.DrawBitmap(_backgroundBitmap, destRect, 1.0f, interpolationMode, srcRect);
            }

            // Obscure everything to the right of the playback cursor if future reveals are toggled off
            if (!_revealFuture && playbackX < Width)
            {
                _renderTarget.FillRectangle(new RawRectangleF(playbackX, 0, Width, Height), _unrevealedBrush);
            }

            _renderTarget.DrawLine(new RawVector2(playbackX, 0), new RawVector2(playbackX, Height), _cursorLineBrush, 1.5f);

            DrawScaleOverlayD2D((float)(Engine.Progress * Width));

            if (_gainOffset != 0.0 && _gainTextFormat != null && _gainBrush != null)
            {
                string sign = _gainOffset > 0 ? "+" : "";
                var textRect = new RawRectangleF(15, 15, 200, 50);
                _renderTarget.DrawText($"Gain: {sign}{_gainOffset:F1} dB", _gainTextFormat, textRect, _gainBrush);
            }

            if (ActiveHoverFrequency.HasValue && _hoverLineBrush != null)
            {
                float hoverY = GetYForFrequency(ActiveHoverFrequency.Value, Height);
                if (hoverY >= 0 && hoverY <= Height)
                {
                    _renderTarget.DrawLine(new RawVector2(0, hoverY), new RawVector2(Width, hoverY), _hoverLineBrush, 1.5f);

                    if (_hoverTextFormat != null && _hoverTextBrush != null)
                    {
                        string label = $"{AudioMath.GetNoteName(ActiveHoverFrequency.Value)} {ActiveHoverFrequency.Value:F1} Hz";
                        var textRect = new RawRectangleF(10, hoverY - 25, Width, hoverY - 2);
                        _renderTarget.DrawText(label, _hoverTextFormat, textRect, _hoverTextBrush);
                    }
                }
            }

            // Neatly overlay "Processing" visual status as a subtle banner rather than obscuring entirely
            if (_isProcessing)
            {
                float boxWidth = 250;
                float boxHeight = 40;
                float boxX = (Width - boxWidth) / 2;
                float boxY = 20;
                var boxRect = new RawRectangleF(boxX, boxY, boxX + boxWidth, boxY + boxHeight);

                if (_statusOverlayBrush != null)
                {
                    _renderTarget.FillRectangle(boxRect, _statusOverlayBrush);
                }
                if (_statusTextFormat != null && _statusBrush != null)
                {
                    _renderTarget.DrawText(_statusMessage, _statusTextFormat, boxRect, _statusBrush);
                }
            }

            _renderTarget.EndDraw();
        }

        private void DrawScaleOverlayD2D(float currentX)
        {
            if (_renderTarget == null || _scaleBgBrush == null || _whiteKeyBrush == null || _blackKeyBrush == null || _cKeyBrush == null || _keyBorderBrush == null)
                return;

            float scaleBoxX = currentX + 2;
            float pianoWidth = 12;
            float textWidth = 40;
            float totalScaleWidth = pianoWidth + textWidth + 5;

            _renderTarget.FillRectangle(new RawRectangleF(scaleBoxX, 0, scaleBoxX + totalScaleWidth, Height), _scaleBgBrush);

            float pianoX = scaleBoxX;
            float textX = pianoX + pianoWidth + 4;

            for (int n = 12; n <= 127; n++)
            {
                double freqTop = 440.0 * Math.Pow(2.0, (n - 69 + 0.5) / 12.0);
                double freqBottom = 440.0 * Math.Pow(2.0, (n - 69 - 0.5) / 12.0);

                if (freqTop < MinFreq || freqBottom > MaxFreq)
                    continue;

                float yTop = GetYForFrequency(freqTop, Height);
                float yBottom = GetYForFrequency(freqBottom, Height);
                float keyHeight = Math.Max(1f, yBottom - yTop);

                bool isC = (n % 12 == 0);
                bool isBlack = (n % 12 == 1 || n % 12 == 3 || n % 12 == 6 || n % 12 == 8 || n % 12 == 10);

                SharpDX.Direct2D1.Brush b = isC ? _cKeyBrush : (isBlack ? _blackKeyBrush : _whiteKeyBrush);
                var keyRect = new RawRectangleF(pianoX, yTop, pianoX + pianoWidth, yTop + keyHeight);

                _renderTarget.FillRectangle(keyRect, b);
                _renderTarget.DrawRectangle(keyRect, _keyBorderBrush, 1f);

                if (isC && _scaleTextFormat != null && _cKeyTextBrush != null)
                {
                    int octave = (n / 12) - 1;
                    string cLabel = $"C{octave}";
                    float yCenter = (yTop + yBottom) / 2f;

                    var cTextRect = new RawRectangleF(textX, yCenter - 10, textX + textWidth, yCenter + 10);
                    _renderTarget.DrawText(cLabel, _scaleTextFormat, cTextRect, _cKeyTextBrush);
                }
            }

            if (_scaleTextFormat != null && _textBrush != null && _tickBrush != null)
            {
                const float ScaleTextOffsetX = 20f;
                foreach (double freq in ScaleFrequencies)
                {
                    if (freq < MinFreq || freq > MaxFreq)
                        continue;
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