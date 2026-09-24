using System;
using System.Drawing;
using System.Windows.Forms;

namespace AmySonicVisualizer
{
    public partial class ViewerForm : Form
    {
        private readonly AudioEngine _audioEngine;

        // Ensure you named the new FftControl `_spectrumControl` in the visual designer
        // and your existing Spectrogram as `_spectrogramControl`

        public ViewerForm(string? initialFilePath = null)
        {
            InitializeComponent();

            Text = "Amysonic Visualizer";
            ClientSize = new Size(1280, 800); // Increased height marginally for both controls

            // 1. Initialize the central Audio Engine
            _audioEngine = new AudioEngine();

            // 2. Bind the user controls to the Engine
            _spectrogramControl.Bind(_audioEngine);
            _spectrumControl.Bind(_audioEngine); // TODO: Bind the new FftControl instance

            // Route form-level keystrokes
            KeyPreview = true;
            KeyDown += ViewerForm_KeyDown;

            // Load initial file if present from startup arguments or dialog
            if (!string.IsNullOrEmpty(initialFilePath))
            {
                _ = _audioEngine.LoadAsync(initialFilePath);
            }
        }

        private void ViewerForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                _audioEngine.TogglePlayback();
            }
        }

        private void _revealAllMenuItem_CheckedChanged(object sender, EventArgs e) 
            => _spectrogramControl.RevealAll = _revealAllMenuItem.Checked;

        private void _smoothSpectrogramToolStripMenuItem_CheckedChanged(object sender, EventArgs e) 
            => _spectrogramControl.SmoothSpectrogram = _smoothSpectrogramToolStripMenuItem.Checked;

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string? filePath = Program.PromptOpenFile();
            if (!string.IsNullOrEmpty(filePath))
            {
                _ = _audioEngine.LoadAsync(filePath);
            }
        }

        private void viewToolStripMenuItem1_CheckedChanged(object sender, EventArgs e)
        {
            // E.g., You can toggle bypass based on menu check states here:
            // _spectrumControl.Bypass = !viewToolStripMenuItem1.Checked;
        }

        protected override void OnClosed(EventArgs e)
        {
            _audioEngine.Dispose();
            base.OnClosed(e);
        }

    }
}