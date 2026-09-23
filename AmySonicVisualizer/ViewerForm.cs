using NAudio.Dsp;
using NAudio.Wave;
using System.Drawing.Imaging;

namespace AmySonicVisualizer
{
    public partial class ViewerForm : Form
    {
        private AudioFileReader? _audioReader;
        private WaveOutEvent? _waveOut;
        private Bitmap? _spectrogramBitmap;
        private readonly System.Windows.Forms.Timer _renderTimer;

        private bool _isProcessing = false;
        private string _statusMessage = "Press [O] or Click to Open Audio File";

        // FFT & Frequency scale settings
        private const int FftSize = 4096;
        private const int FftBits = 12; // 2^12 = 4096
        private const double MinFreq = 40.0;     // ~E1 (bass floor)
        private const double MaxFreq = 8000.0;   // Melodic ceiling
        private const double MinDb = -75.0;
        private const double MaxDb = -5.0;

        public ViewerForm()
        {
            Text = "Melodic Revealing Spectrogram";
            ClientSize = new Size(1280, 600);
            BackColor = Color.FromArgb(15, 15, 18);
            DoubleBuffered = true;

            _renderTimer = new System.Windows.Forms.Timer { Interval = 20 };
            _renderTimer.Tick += (s, e) => Invalidate();

            KeyDown += OnKeyDown;
            MouseDown += OnMouseDown;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.O)
            {
                OpenFile();
            }
            else if (e.KeyCode == Keys.Space && _waveOut != null)
            {
                if (_waveOut.PlaybackState == PlaybackState.Playing)
                    _waveOut.Pause();
                else
                    _waveOut.Play();
            }
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (_spectrogramBitmap == null && !_isProcessing)
            {
                OpenFile();
                return;
            }

            // Click to seek
            if (_audioReader != null && _waveOut != null && ClientSize.Width > 0)
            {
                double progress = Math.Clamp((double)e.X / ClientSize.Width, 0.0, 1.0);
                _audioReader.CurrentTime = TimeSpan.FromSeconds(progress * _audioReader.TotalTime.TotalSeconds);
            }
        }

        private async void OpenFile()
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Audio Files (*.mp3;*.wav;*.flac)|*.mp3;*.wav;*.flac"
            };

            if (ofd.ShowDialog() != DialogResult.OK) return;

            _renderTimer.Stop();
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _audioReader?.Dispose();
            _spectrogramBitmap?.Dispose();
            _spectrogramBitmap = null;

            _isProcessing = true;
            _statusMessage = "Analyzing melodic frequencies across track...";
            Invalidate();

            int width = ClientSize.Width;
            int height = ClientSize.Height;
            string path = ofd.FileName;

            try
            {
                _spectrogramBitmap = await Task.Run(() => GenerateSpectrogram(path, width, height));

                _audioReader = new AudioFileReader(path);
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_audioReader);
                _waveOut.Play();
                _renderTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to process audio: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
                Invalidate();
            }
        }
        private Bitmap GenerateSpectrogram(string filePath, int width, int height)
        {
            using var reader = new AudioFileReader(filePath);
            int channels = reader.WaveFormat.Channels;
            int sampleRate = reader.WaveFormat.SampleRate;

            // 1. Read all samples downmixed to mono
            var monoSamples = new List<float>((int)(reader.Length / (sizeof(float) * channels)));
            float[] buffer = new float[sampleRate * channels];
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i += channels)
                {
                    float sum = 0;
                    for (int c = 0; c < channels; c++) sum += buffer[i + c];
                    monoSamples.Add(sum / channels);
                }
            }

            var bmp = new Bitmap(width, height, PixelFormat.Format32bppRgb);
            var complexBuffer = new Complex[FftSize];

            // Precompute frequency mapping per vertical row y
            int[] rowToBin = new int[height];
            for (int y = 0; y < height; y++)
            {
                // Logarithmic scale: y = 0 (top/high freq) to y = height - 1 (bottom/low freq)
                double normY = (double)(height - 1 - y) / height;
                double freq = MinFreq * Math.Pow(MaxFreq / MinFreq, normY);
                int bin = (int)Math.Round(freq * FftSize / sampleRate);
                rowToBin[y] = Math.Clamp(bin, 0, (FftSize / 2) - 1);
            }

            // 2. Compute FFT for each horizontal pixel column
            for (int x = 0; x < width; x++)
            {
                long centerSample = (long)x * monoSamples.Count / width;
                long startSample = centerSample - (FftSize / 2);

                for (int i = 0; i < FftSize; i++)
                {
                    long sampleIdx = startSample + i;
                    float sampleVal = (sampleIdx >= 0 && sampleIdx < monoSamples.Count) ? monoSamples[(int)sampleIdx] : 0f;

                    // Calculate and apply the Hann window multiplier for the current index
                    float windowMultiplier = (float)FastFourierTransform.HannWindow(i, FftSize);

                    complexBuffer[i].X = sampleVal * windowMultiplier;
                    complexBuffer[i].Y = 0f;
                }

                FastFourierTransform.FFT(true, FftBits, complexBuffer);

                // Render vertical column
                for (int y = 0; y < height; y++)
                {
                    int bin = rowToBin[y];
                    double real = complexBuffer[bin].X;
                    double imag = complexBuffer[bin].Y;
                    double mag = Math.Sqrt(real * real + imag * imag);

                    double db = 20.0 * Math.Log10(Math.Max(mag, 1e-6));
                    float norm = Math.Clamp((float)((db - MinDb) / (MaxDb - MinDb)), 0f, 1f);

                    bmp.SetPixel(x, y, GetSpectralColor(norm));
                }
            }

            return bmp;
        }

        // Melodic heat-map palette: Black -> Deep Purple -> Magenta -> Electric Cyan -> White
        private static Color GetSpectralColor(float intensity)
        {
            if (intensity <= 0.0f) return Color.FromArgb(12, 10, 18);

            int r, g, b;
            if (intensity < 0.25f)
            {
                float t = intensity / 0.25f;
                r = (int)(45 * t);
                g = 0;
                b = (int)(90 * t);
            }
            else if (intensity < 0.55f)
            {
                float t = (intensity - 0.25f) / 0.30f;
                r = (int)(45 + (180 - 45) * t);
                g = (int)(15 * t);
                b = (int)(90 + (130 - 90) * t);
            }
            else if (intensity < 0.85f)
            {
                float t = (intensity - 0.55f) / 0.30f;
                r = (int)(180 + (40 - 180) * t);
                g = (int)(15 + (210 - 15) * t);
                b = (int)(220 + (255 - 220) * t);
            }
            else
            {
                float t = (intensity - 0.85f) / 0.15f;
                r = (int)(40 + (255 - 40) * t);
                g = (int)(210 + (255 - 210) * t);
                b = 255;
            }

            return Color.FromArgb(r, g, b);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_isProcessing || _spectrogramBitmap == null || _audioReader == null)
            {
                using var brush = new SolidBrush(Color.FromArgb(180, 180, 190));
                using var font = new Font("Segoe UI", 12f, FontStyle.Regular);
                var size = e.Graphics.MeasureString(_statusMessage, font);
                e.Graphics.DrawString(_statusMessage, font, brush, (ClientSize.Width - size.Width) / 2, (ClientSize.Height - size.Height) / 2);
                return;
            }

            double progress = Math.Clamp(_audioReader.CurrentTime.TotalSeconds / _audioReader.TotalTime.TotalSeconds, 0.0, 1.0);
            int currentX = (int)(progress * ClientSize.Width);

            // 1. Draw revealed spectrogram: ONLY from column 0 to currentX
            if (currentX > 0)
            {
                var srcRect = new Rectangle(0, 0, currentX, _spectrogramBitmap.Height);
                var destRect = new Rectangle(0, 0, currentX, ClientSize.Height);
                e.Graphics.DrawImage(_spectrogramBitmap, destRect, srcRect, GraphicsUnit.Pixel);
            }

            // 2. Future mask: Keep everything to the right completely pitch black
            if (currentX < ClientSize.Width)
            {
                using var blackBrush = new SolidBrush(Color.FromArgb(10, 10, 14));
                e.Graphics.FillRectangle(blackBrush, currentX, 0, ClientSize.Width - currentX, ClientSize.Height);
            }

            // 3. Playhead cursor line
            using var linePen = new Pen(Color.FromArgb(235, 235, 245), 1.5f);
            e.Graphics.DrawLine(linePen, currentX, 0, currentX, ClientSize.Height);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _renderTimer.Stop();
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _audioReader?.Dispose();
            _spectrogramBitmap?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
