using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using NAudio.Dsp;

namespace AmySonicVisualizer
{
    public class FftControl : VisualizerControlBase
    {
        private const int FftSize = 4096;
        private const int FftBits = 12; // 2^12 = 4096

        [Category("FFT Settings")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public double MinFreq { get; set; } = 40.0;

        [Category("FFT Settings")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public double MaxFreq { get; set; } = 8000.0;

        [Category("FFT Settings")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public double MinDb { get; set; } = -75.0;

        [Category("FFT Settings")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public double MaxDb { get; set; } = -5.0;

        [Category("FFT Settings")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double[] ScaleFrequencies { get; set; } = { 40, 50, 100, 200, 500, 1000, 2000, 5000, 8000 };

        public FftControl()
        {
            BackColor = Color.FromArgb(15, 15, 18);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Bypass) return;
            base.OnPaint(e);

            var g = e.Graphics;
            DrawGrid(g);

            if (Engine == null || !Engine.IsLoaded)
            {
                DrawStatus(g, Engine?.IsLoading == true ? "Loading audio data..." : "Awaiting Audio");
                return;
            }

            var monoSamples = Engine.MonoSamples;
            long centerSample = (long)(Engine.Progress * monoSamples.Length);
            long startSample = centerSample - (FftSize / 2);

            var complexBuffer = new Complex[FftSize];
            for (int i = 0; i < FftSize; i++)
            {
                long sampleIdx = startSample + i;
                float sampleVal = (sampleIdx >= 0 && sampleIdx < monoSamples.Length) ? monoSamples[sampleIdx] : 0f;
                float windowMultiplier = (float)FastFourierTransform.HannWindow(i, FftSize);

                complexBuffer[i].X = sampleVal * windowMultiplier;
                complexBuffer[i].Y = 0f;
            }

            FastFourierTransform.FFT(true, FftBits, complexBuffer);

            using var pen = new Pen(Color.FromArgb(180, 30, 220), 2f);
            using var brush = new SolidBrush(Color.FromArgb(80, 180, 30, 220));
            g.SmoothingMode = SmoothingMode.AntiAlias;

            PointF[] points = new PointF[Width];
            for (int x = 0; x < Width; x++)
            {
                double normX = (double)x / (Width - 1);

                // Logarithmic mapping for X-axis (evenly spaced octaves)
                double freq = MinFreq * Math.Pow(MaxFreq / MinFreq, normX);
                int bin = (int)Math.Round(freq * FftSize / Engine.SampleRate);
                bin = Math.Clamp(bin, 0, (FftSize / 2) - 1);

                double real = complexBuffer[bin].X;
                double imag = complexBuffer[bin].Y;
                double mag = Math.Sqrt(real * real + imag * imag);
                double db = 20.0 * Math.Log10(Math.Max(mag, 1e-6));

                float normY = Math.Clamp((float)((db - MinDb) / (MaxDb - MinDb)), 0f, 1f);
                float y = Height - (normY * Height);

                points[x] = new PointF(x, y);
            }

            if (points.Length > 1)
            {
                // Draw filled polygon connecting to the bottom
                var polyPoints = new PointF[points.Length + 2];
                Array.Copy(points, polyPoints, points.Length);
                polyPoints[points.Length] = new PointF(Width, Height);
                polyPoints[points.Length + 1] = new PointF(0, Height);

                g.FillPolygon(brush, polyPoints);
                g.DrawLines(pen, points);
            }
        }

        private void DrawGrid(Graphics g)
        {
            using var gridPen = new Pen(Color.FromArgb(30, 30, 35), 1f);
            using var textBrush = new SolidBrush(Color.FromArgb(100, 100, 110));
            using var font = new Font("Consolas", 8f);

            foreach (var freq in ScaleFrequencies)
            {
                if (freq < MinFreq || freq > MaxFreq) continue;
                double normX = Math.Log(freq / MinFreq) / Math.Log(MaxFreq / MinFreq);
                int x = (int)(normX * Width);

                g.DrawLine(gridPen, x, 0, x, Height);
                string label = freq >= 1000 ? $"{(freq / 1000)}k" : freq.ToString();
                g.DrawString(label, font, textBrush, x + 4, Height - 20);
            }
        }

        private void DrawStatus(Graphics g, string message)
        {
            using var brush = new SolidBrush(Color.FromArgb(180, 180, 190));
            using var font = new Font("Segoe UI", 12f, FontStyle.Regular);
            var size = g.MeasureString(message, font);
            g.DrawString(message, font, brush, (Width - size.Width) / 2, (Height - size.Height) / 2);
        }
    }
}