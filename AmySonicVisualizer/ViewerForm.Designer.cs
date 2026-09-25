using AmySonicVisualizer.VisualizerControls;

namespace AmySonicVisualizer
{
    partial class ViewerForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            _spectrogramControl = new SpectrogramVisualizerControl();
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            openToolStripMenuItem = new ToolStripMenuItem();
            _analysisToolStripMenuItem = new ToolStripMenuItem();
            _spectrogramMethodToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            _spectrogramFftWindowToolStripMenuItem = new ToolStripMenuItem();
            viewToolStripMenuItem = new ToolStripMenuItem();
            _revealAllMenuItem = new ToolStripMenuItem();
            _smoothSpectrogramToolStripMenuItem = new ToolStripMenuItem();
            _smoothSpectrumGraphToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            splitContainer1 = new SplitContainer();
            _spectrumControl = new SpectrumVisualizerControl();
            toolStripContainer1 = new ToolStripContainer();
            menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            toolStripContainer1.ContentPanel.SuspendLayout();
            toolStripContainer1.TopToolStripPanel.SuspendLayout();
            toolStripContainer1.SuspendLayout();
            SuspendLayout();
            // 
            // _spectrogramControl
            // 
            _spectrogramControl.BackColor = Color.FromArgb(15, 15, 18);
            _spectrogramControl.Bypass = false;
            _spectrogramControl.CqtBinsPerOctave = 120;
            _spectrogramControl.Dock = DockStyle.Fill;
            _spectrogramControl.Location = new Point(0, 0);
            _spectrogramControl.Margin = new Padding(3, 2, 3, 2);
            _spectrogramControl.MaxDb = -5D;
            _spectrogramControl.MaxFreq = 8000D;
            _spectrogramControl.MinDb = -75D;
            _spectrogramControl.MinFreq = 32.7D;
            _spectrogramControl.Name = "_spectrogramControl";
            _spectrogramControl.Size = new Size(892, 286);
            _spectrogramControl.TabIndex = 0;
            // 
            // menuStrip1
            // 
            menuStrip1.Dock = DockStyle.None;
            menuStrip1.ImageScalingSize = new Size(18, 18);
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, _analysisToolStripMenuItem, viewToolStripMenuItem, helpToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Padding = new Padding(5, 2, 0, 2);
            menuStrip1.Size = new Size(892, 24);
            menuStrip1.TabIndex = 2;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "File";
            // 
            // openToolStripMenuItem
            // 
            openToolStripMenuItem.Name = "openToolStripMenuItem";
            openToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            openToolStripMenuItem.Size = new Size(146, 22);
            openToolStripMenuItem.Text = "Open";
            openToolStripMenuItem.Click += openToolStripMenuItem_Click;
            // 
            // _analysisToolStripMenuItem
            // 
            _analysisToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { _spectrogramMethodToolStripMenuItem, toolStripSeparator1, _spectrogramFftWindowToolStripMenuItem });
            _analysisToolStripMenuItem.Name = "_analysisToolStripMenuItem";
            _analysisToolStripMenuItem.Size = new Size(62, 20);
            _analysisToolStripMenuItem.Text = "Analysis";
            // 
            // _spectrogramMethodToolStripMenuItem
            // 
            _spectrogramMethodToolStripMenuItem.Name = "_spectrogramMethodToolStripMenuItem";
            _spectrogramMethodToolStripMenuItem.Size = new Size(180, 22);
            _spectrogramMethodToolStripMenuItem.Text = "Method";
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(177, 6);
            // 
            // _spectrogramFftWindowToolStripMenuItem
            // 
            _spectrogramFftWindowToolStripMenuItem.Name = "_spectrogramFftWindowToolStripMenuItem";
            _spectrogramFftWindowToolStripMenuItem.Size = new Size(180, 22);
            _spectrogramFftWindowToolStripMenuItem.Text = "FFT Window";
            // 
            // viewToolStripMenuItem
            // 
            viewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { _revealAllMenuItem, _smoothSpectrogramToolStripMenuItem, _smoothSpectrumGraphToolStripMenuItem });
            viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            viewToolStripMenuItem.Size = new Size(44, 20);
            viewToolStripMenuItem.Text = "View";
            // 
            // _revealAllMenuItem
            // 
            _revealAllMenuItem.CheckOnClick = true;
            _revealAllMenuItem.Name = "_revealAllMenuItem";
            _revealAllMenuItem.ShortcutKeys = Keys.Control | Keys.F;
            _revealAllMenuItem.Size = new Size(205, 22);
            _revealAllMenuItem.Text = "Reveal Future";
            _revealAllMenuItem.CheckedChanged += _revealAllMenuItem_CheckedChanged;
            // 
            // _smoothSpectrogramToolStripMenuItem
            // 
            _smoothSpectrogramToolStripMenuItem.Checked = true;
            _smoothSpectrogramToolStripMenuItem.CheckOnClick = true;
            _smoothSpectrogramToolStripMenuItem.CheckState = CheckState.Checked;
            _smoothSpectrogramToolStripMenuItem.Name = "_smoothSpectrogramToolStripMenuItem";
            _smoothSpectrogramToolStripMenuItem.Size = new Size(205, 22);
            _smoothSpectrogramToolStripMenuItem.Text = "Smooth Spectrogram";
            _smoothSpectrogramToolStripMenuItem.CheckedChanged += _smoothSpectrogramToolStripMenuItem_CheckedChanged;
            // 
            // _smoothSpectrumGraphToolStripMenuItem
            // 
            _smoothSpectrumGraphToolStripMenuItem.Checked = true;
            _smoothSpectrumGraphToolStripMenuItem.CheckOnClick = true;
            _smoothSpectrumGraphToolStripMenuItem.CheckState = CheckState.Checked;
            _smoothSpectrumGraphToolStripMenuItem.Name = "_smoothSpectrumGraphToolStripMenuItem";
            _smoothSpectrumGraphToolStripMenuItem.Size = new Size(205, 22);
            _smoothSpectrumGraphToolStripMenuItem.Text = "Smooth Spectrum Graph";
            _smoothSpectrumGraphToolStripMenuItem.CheckedChanged += smoothSpectrumGraphToolStripMenuItem_CheckedChanged;
            // 
            // helpToolStripMenuItem
            // 
            helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { aboutToolStripMenuItem });
            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            helpToolStripMenuItem.Size = new Size(44, 20);
            helpToolStripMenuItem.Text = "Help";
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new Size(107, 22);
            aboutToolStripMenuItem.Text = "About";
            aboutToolStripMenuItem.Click += aboutToolStripMenuItem_Click;
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 0);
            splitContainer1.Margin = new Padding(3, 2, 3, 2);
            splitContainer1.Name = "splitContainer1";
            splitContainer1.Orientation = Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(_spectrogramControl);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(_spectrumControl);
            splitContainer1.Size = new Size(892, 355);
            splitContainer1.SplitterDistance = 286;
            splitContainer1.SplitterWidth = 3;
            splitContainer1.TabIndex = 3;
            // 
            // _spectrumControl
            // 
            _spectrumControl.BackColor = Color.FromArgb(15, 15, 18);
            _spectrumControl.Bypass = false;
            _spectrumControl.Dock = DockStyle.Fill;
            _spectrumControl.Location = new Point(0, 0);
            _spectrumControl.Margin = new Padding(3, 2, 3, 2);
            _spectrumControl.MaxDb = -5D;
            _spectrumControl.MaxFreq = 20000D;
            _spectrumControl.MinDb = -75D;
            _spectrumControl.MinFreq = 20D;
            _spectrumControl.Name = "_spectrumControl";
            _spectrumControl.Size = new Size(892, 66);
            _spectrumControl.TabIndex = 0;
            _spectrumControl.Text = "fftControl1";
            // 
            // toolStripContainer1
            // 
            // 
            // toolStripContainer1.ContentPanel
            // 
            toolStripContainer1.ContentPanel.Controls.Add(splitContainer1);
            toolStripContainer1.ContentPanel.Size = new Size(892, 355);
            toolStripContainer1.Dock = DockStyle.Fill;
            toolStripContainer1.Location = new Point(0, 0);
            toolStripContainer1.Name = "toolStripContainer1";
            toolStripContainer1.Size = new Size(892, 379);
            toolStripContainer1.TabIndex = 4;
            toolStripContainer1.Text = "toolStripContainer1";
            // 
            // toolStripContainer1.TopToolStripPanel
            // 
            toolStripContainer1.TopToolStripPanel.Controls.Add(menuStrip1);
            // 
            // ViewerForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(892, 379);
            Controls.Add(toolStripContainer1);
            MainMenuStrip = menuStrip1;
            Margin = new Padding(3, 2, 3, 2);
            Name = "ViewerForm";
            Text = "Form1";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            toolStripContainer1.ContentPanel.ResumeLayout(false);
            toolStripContainer1.TopToolStripPanel.ResumeLayout(false);
            toolStripContainer1.TopToolStripPanel.PerformLayout();
            toolStripContainer1.ResumeLayout(false);
            toolStripContainer1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private SpectrogramVisualizerControl _spectrogramControl;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem viewToolStripMenuItem;
        private ToolStripMenuItem _revealAllMenuItem;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem openToolStripMenuItem;
        private SplitContainer splitContainer1;
        private SpectrumVisualizerControl _spectrumControl;
        private ToolStripMenuItem _smoothSpectrogramToolStripMenuItem;
		private ToolStripMenuItem _smoothSpectrumGraphToolStripMenuItem;
		private ToolStripContainer toolStripContainer1;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private ToolStripMenuItem _analysisToolStripMenuItem;
        private ToolStripMenuItem _spectrogramMethodToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem _spectrogramFftWindowToolStripMenuItem;
    }
}
