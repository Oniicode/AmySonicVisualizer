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
            viewToolStripMenuItem = new ToolStripMenuItem();
            _revealAllMenuItem = new ToolStripMenuItem();
            splitContainer1 = new SplitContainer();
            _spectrumControl = new SpectrumVisualizerControl();
            menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            SuspendLayout();
            // 
            // _spectrogramControl
            // 
            _spectrogramControl.BackColor = Color.FromArgb(15, 15, 18);
            _spectrogramControl.Bypass = false;
            _spectrogramControl.Dock = DockStyle.Fill;
            _spectrogramControl.Location = new Point(0, 0);
            _spectrogramControl.MaxDb = -5D;
            _spectrogramControl.MaxFreq = 8000D;
            _spectrogramControl.MinDb = -75D;
            _spectrogramControl.MinFreq = 40D;
            _spectrogramControl.Name = "_spectrogramControl";
            _spectrogramControl.Size = new Size(1020, 366);
            _spectrogramControl.TabIndex = 0;
            // 
            // menuStrip1
            // 
            menuStrip1.ImageScalingSize = new Size(18, 18);
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, viewToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1020, 27);
            menuStrip1.TabIndex = 2;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(41, 23);
            fileToolStripMenuItem.Text = "File";
            // 
            // openToolStripMenuItem
            // 
            openToolStripMenuItem.Name = "openToolStripMenuItem";
            openToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            openToolStripMenuItem.Size = new Size(171, 24);
            openToolStripMenuItem.Text = "Open";
            openToolStripMenuItem.Click += openToolStripMenuItem_Click;
            // 
            // viewToolStripMenuItem
            // 
            viewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { _revealAllMenuItem });
            viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            viewToolStripMenuItem.Size = new Size(50, 23);
            viewToolStripMenuItem.Text = "View";
            // 
            // _revealAllMenuItem
            // 
            _revealAllMenuItem.CheckOnClick = true;
            _revealAllMenuItem.Name = "_revealAllMenuItem";
            _revealAllMenuItem.ShortcutKeys = Keys.Control | Keys.R;
            _revealAllMenuItem.Size = new Size(192, 24);
            _revealAllMenuItem.Text = "Reveal All";
            _revealAllMenuItem.CheckedChanged += _revealAllMenuItem_CheckedChanged;
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 27);
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
            splitContainer1.Size = new Size(1020, 453);
            splitContainer1.SplitterDistance = 366;
            splitContainer1.TabIndex = 3;
            // 
            // _spectrumControl
            // 
            _spectrumControl.BackColor = Color.FromArgb(15, 15, 18);
            _spectrumControl.Bypass = false;
            _spectrumControl.Dock = DockStyle.Fill;
            _spectrumControl.Location = new Point(0, 0);
            _spectrumControl.MaxDb = -5D;
            _spectrumControl.MaxFreq = 8000D;
            _spectrumControl.MinDb = -75D;
            _spectrumControl.MinFreq = 40D;
            _spectrumControl.Name = "_spectrumControl";
            _spectrumControl.Size = new Size(1020, 83);
            _spectrumControl.TabIndex = 0;
            _spectrumControl.Text = "fftControl1";
            // 
            // ViewerForm
            // 
            AutoScaleDimensions = new SizeF(8F, 19F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1020, 480);
            Controls.Add(splitContainer1);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "ViewerForm";
            Text = "Form1";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
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
    }
}
