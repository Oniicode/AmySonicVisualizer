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

		public ViewerForm(string? initialFilePath = null)
		{
			InitializeComponent();

			Text = _programName;
			ClientSize = new Size(1280, 800);

			_audioEngine = new AudioEngine();

			// Subscribe to FileLoaded event to update titlebar
			_audioEngine.FileLoading += AudioEngine_FileLoading;

			_spectrogramControl.Bind(_audioEngine);
			_spectrumControl.Bind(_audioEngine);

			KeyPreview = true;
			KeyDown += ViewerForm_KeyDown;

			InitializeFftSizeMenu();

			// Load initial file if present from startup arguments or dialog
			if (!string.IsNullOrEmpty(initialFilePath))
			{
				_ = _audioEngine.LoadAsync(initialFilePath);
			}
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
			// E.g., You can toggle bypass based on menu check states here:
			// _spectrumControl.Bypass = !viewToolStripMenuItem1.Checked;
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