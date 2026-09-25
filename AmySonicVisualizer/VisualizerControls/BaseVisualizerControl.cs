using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace AmySonicVisualizer.VisualizerControls
{
    /// <summary>
    /// Base class for all visualizer controls that plug into the AudioEngine.
    /// Provides the Bypass toggle, shared render loop functionality, and cross-referencing utilities.
    /// </summary>
    public abstract class BaseVisualizerControl : ContainerControl
    {
        private bool _bypass = false;
        private double? _activeHoverFrequency;

        protected readonly System.Windows.Forms.Timer RenderTimer;

        [Browsable(false)]
        protected AudioEngine? Engine { get; private set; }

        /// <summary>
        /// Fires when the user hovers over a specific frequency in this visualizer.
        /// </summary>
        public event EventHandler<double?>? HoverFrequencyChanged;

        /// <summary>
        /// When set, forces this control to draw a reference line at the given frequency.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double? ActiveHoverFrequency
        {
            get => _activeHoverFrequency;
            set
            {
                if (_activeHoverFrequency != value)
                {
                    _activeHoverFrequency = value;
                    if (!Bypass) Invalidate(); // Force a redraw when the referenced frequency changes
                }
            }
        }

        [Category("Behavior")]
        [Description("When true, the control skips rendering to save resources.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool Bypass
        {
            get => _bypass;
            set
            {
                _bypass = value;
                if (!_bypass) Invalidate(); // Force a redraw when un-bypassed
            }
        }

        public BaseVisualizerControl()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;

            RenderTimer = new System.Windows.Forms.Timer { Interval = 20 }; // ~50 FPS
            RenderTimer.Tick += (s, e) =>
            {
                if (!Bypass && Engine != null && (Engine.IsLoaded || Engine.IsLoading))
                {
                    Invalidate();
                }
            };
        }

        public virtual void Bind(AudioEngine engine)
        {
            Engine = engine;
            RenderTimer.Start();
        }

        protected void OnHoverFrequencyChanged(double? frequency)
        {
            HoverFrequencyChanged?.Invoke(this, frequency);
        }

        protected string GetNoteName(double frequency)
        {
            if (frequency <= 0) return string.Empty;

            // Convert frequency to MIDI note number (69 is A4 / 440 Hz)
            int noteNumber = (int)Math.Round(12 * Math.Log2(frequency / 440.0) + 69);
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

            int octave = (noteNumber / 12) - 1;
            int noteIndex = noteNumber % 12;

            // Handle potential negative indices for extremely low frequencies
            if (noteIndex < 0)
            {
                noteIndex += 12;
                octave--;
            }

            return $"{noteNames[noteIndex]}{octave}";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                RenderTimer?.Stop();
                RenderTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}