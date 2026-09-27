using AmySonicVisualizer.VisualizerControls;
using System.Reflection;

namespace AmySonicVisualizer
{
    public partial class ViewerForm : Form
    {
        private readonly AudioEngine _audioEngine;
        private readonly string _programName = "Amysonic Visualizer";

        public ViewerForm()
        {
            InitializeComponent();

            Text = _programName;
            ClientSize = new Size(1280, 800);

            // Enable Drag and Drop
            AllowDrop = true;
            DragEnter += ViewerForm_DragEnter;
            DragDrop += ViewerForm_DragDrop;

            _audioEngine = new AudioEngine();
            _audioEngine.FileLoading += AudioEngine_FileLoading;

            _spectrogramControl.BindEngine(_audioEngine);
            _spectrumControl.BindEngine(_audioEngine);

            _spectrogramControl.HoverFrequencyChanged += (s, freq) =>
            {
                _spectrogramControl.ActiveHoverFrequency = freq;
                _spectrumControl.ActiveHoverFrequency = freq;
            };
            _spectrumControl.HoverFrequencyChanged += (s, freq) =>
            {
                _spectrogramControl.ActiveHoverFrequency = freq;
                _spectrumControl.ActiveHoverFrequency = freq;
            };

            KeyPreview = true;
            KeyDown += ViewerForm_KeyDown;

            InitializeFftSizeMenu();
            InitializeSpectrogramMethodMenu();
        }


        public async Task LoadFileAsync(string filePath)
        {
            if (filePath == null)
                return;

            await _audioEngine.LoadAsync(filePath);
        }

        private void ViewerForm_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy; 
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void ViewerForm_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
                return;

            string filePath = files[0];
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (Program.SupportedExtensions.Contains(ext))
            {
                _ = _audioEngine.LoadAsync(filePath);
            }
            else
            {
                MessageBox.Show($"Unsupported file format. Please drop one of the following: {string.Join(", ", Program.SupportedExtensions)}",
                    "Unsupported File", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void InitializeFftSizeMenu()
        {
            _spectrogramFftWindowToolStripMenuItem.DropDownItems.Clear();

            for (int size = 256; size <= FftSpectrogramAnalyzer.MaxFftSize; size <<= 1)
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

        private void InitializeSpectrogramMethodMenu()
        {
            _spectrogramMethodToolStripMenuItem.DropDownItems.Clear();

            foreach (SpectrogramAlgorithmType method in Enum.GetValues(typeof(SpectrogramAlgorithmType)))
            {
                var item = new ToolStripMenuItem
                {
                    Text = method.ToString(),
                    Tag = method,
                    Checked = (_spectrogramControl.AnalysisMethod == method),
                    ShortcutKeys = method switch
                    {
                        SpectrogramAlgorithmType.FFT => Keys.Control | Keys.Shift | Keys.F,
                        SpectrogramAlgorithmType.CQT => Keys.Control | Keys.Shift | Keys.C,
                        _ => Keys.None
                    }
                };

                item.Click += (sender, e) =>
                {
                    if (sender is ToolStripMenuItem clickedItem && clickedItem.Tag is SpectrogramAlgorithmType newMethod)
                    {
                        _spectrogramControl.AnalysisMethod = newMethod;

                        foreach (ToolStripMenuItem sibling in _spectrogramMethodToolStripMenuItem.DropDownItems)
                            sibling.Checked = (sibling == clickedItem);
                    }
                };

                _spectrogramMethodToolStripMenuItem.DropDownItems.Add(item);
            }
        }

        private void ViewerForm_KeyDown(object? sender, KeyEventArgs e)
        {
            switch(e.KeyCode)
            {
                case Keys.Space:
                    _audioEngine.TogglePlayback();
                    break;
                case Keys.Home:
                    _audioEngine.Progress = 0.0;
                    break;
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

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show($"{_programName}\nVersion {Assembly.GetExecutingAssembly().GetName().Version}\nMade by Amy for Amies 🐈‍⬛");
        }
    }
}
