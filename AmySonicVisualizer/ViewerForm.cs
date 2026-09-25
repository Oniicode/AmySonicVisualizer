using AmySonicVisualizer.VisualizerControls;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace AmySonicVisualizer
{
    public partial class ViewerForm : Form
    {
        private readonly AudioEngine _audioEngine;
        private readonly string _programName = "Amysonic Visualizer";

        private readonly ToolStripLabel _inspectionLabel = new()
        {
            Text = string.Empty,
            Alignment = ToolStripItemAlignment.Right
        };

        public ViewerForm(string? initialFilePath = null)
        {
            InitializeComponent();

            Text = _programName;
            ClientSize = new Size(1280, 800);

            _audioEngine = new AudioEngine();
            _audioEngine.FileLoading += AudioEngine_FileLoading;

            _spectrogramControl.Bind(_audioEngine);
            _spectrumControl.Bind(_audioEngine);

            // Wire up cross-referencing hover capability between the visualizers.
            // When one control fires a hover, we update BOTH controls' ActiveHoverFrequency
            // so that the line mirrors gracefully across all contexts.
            _spectrogramControl.HoverFrequencyChanged += (s, freq) =>
            {
                _spectrogramControl.ActiveHoverFrequency = freq;
                _spectrumControl.ActiveHoverFrequency = freq;
                UpdateInspectionLabel(freq);
            };

            _spectrumControl.HoverFrequencyChanged += (s, freq) =>
            {
                _spectrogramControl.ActiveHoverFrequency = freq;
                _spectrumControl.ActiveHoverFrequency = freq;
                UpdateInspectionLabel(freq);
            };

            KeyPreview = true;
            KeyDown += ViewerForm_KeyDown;

            InitializeFftSizeMenu();

            menuStrip1.Items.Add(_inspectionLabel);

            if (!string.IsNullOrEmpty(initialFilePath))
            {
                _ = _audioEngine.LoadAsync(initialFilePath);
            }
        }

        private void UpdateInspectionLabel(double? frequency)
        {
            if (frequency.HasValue)
            {
                _inspectionLabel.Text = $"{GetNoteName(frequency.Value)} {frequency.Value:F1} Hz";
            }
            else
            {
                _inspectionLabel.Text = string.Empty;
            }
        }

        private string GetNoteName(double frequency)
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

        private void InitializeFftSizeMenu()
        {
            _spectrogramFftWindowToolStripMenuItem.DropDownItems.Clear();

            for (int size = 256; size <= SpectrogramVisualizerControl.MaxFftSize; size <<= 1)
            {
                var item = new ToolStripMenuItem
                {
                    Text = size.ToString(),
                    Tag = size,
                    Checked = (_spectrogramControl.FftSize == size)
                };

                item.Click += (sender, e) =>
                {
                    if (sender is ToolStripMenuItem clickedItem && clickedItem.Tag is int newSize)
                    {
                        _spectrogramControl.FftSize = newSize;

                        foreach (ToolStripMenuItem sibling in _spectrogramFftWindowToolStripMenuItem.DropDownItems)
                            sibling.Checked = (sibling == clickedItem);
                    }
                };

                _spectrogramFftWindowToolStripMenuItem.DropDownItems.Add(item);
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
            => _spectrogramControl.RevealFuture = _revealAllMenuItem.Checked;

        private void _smoothSpectrogramToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
            => _spectrogramControl.SmoothSpectrogram = _smoothSpectrogramToolStripMenuItem.Checked;

        private void smoothSpectrumGraphToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
            => _spectrumControl.SmoothForeground = _smoothSpectrumGraphToolStripMenuItem.Checked;

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
            // Example placeholder behavior block
        }

        private void AudioEngine_FileLoading(object? sender, EventArgs e)
        {
            UpdateTitlebar();
        }

        private void UpdateTitlebar()
        {
            if (!string.IsNullOrEmpty(_audioEngine.CurrentFilePath))
            {
                string fileName = System.IO.Path.GetFileName(_audioEngine.CurrentFilePath);
                Text = $"{fileName} - {_programName}";
            }
            else
            {
                Text = _programName;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _audioEngine.Dispose();
            base.OnClosed(e);
        }
    }
}