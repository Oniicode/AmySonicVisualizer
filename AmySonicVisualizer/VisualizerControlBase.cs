using System.ComponentModel;
using System.Windows.Forms;

namespace AmySonicVisualizer
{
    /// <summary>
    /// Base class for all visualizer controls that plug into the AudioEngine.
    /// Provides the Bypass toggle and shared render loop functionality.
    /// </summary>
    public abstract class VisualizerControlBase : ContainerControl
    {
        private bool _bypass = false;

        protected readonly System.Windows.Forms.Timer RenderTimer;

        [Browsable(false)]
        protected AudioEngine? Engine { get; private set; }

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

        public VisualizerControlBase()
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