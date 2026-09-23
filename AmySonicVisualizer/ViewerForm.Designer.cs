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
            _spectrogramControl = new SpectrogramControl();
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            openToolStripMenuItem = new ToolStripMenuItem();
            viewToolStripMenuItem = new ToolStripMenuItem();
            _revealAllMenuItem = new ToolStripMenuItem();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // _spectrogramControl
            // 
            _spectrogramControl.BackColor = Color.FromArgb(15, 15, 18);
            _spectrogramControl.Dock = DockStyle.Fill;
            _spectrogramControl.Location = new Point(0, 27);
            _spectrogramControl.MaxDb = -5D;
            _spectrogramControl.MaxFreq = 8000D;
            _spectrogramControl.MinDb = -75D;
            _spectrogramControl.MinFreq = 40D;
            _spectrogramControl.Name = "_spectrogramControl";
            _spectrogramControl.Size = new Size(1020, 423);
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
            _revealAllMenuItem.Size = new Size(202, 24);
            _revealAllMenuItem.Text = "Reveal All";
            _revealAllMenuItem.CheckedChanged += _revealAllMenuItem_CheckedChanged;
            // 
            // ViewerForm
            // 
            AutoScaleDimensions = new SizeF(8F, 19F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1020, 450);
            Controls.Add(_spectrogramControl);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "ViewerForm";
            Text = "Form1";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private SpectrogramControl _spectrogramControl;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem viewToolStripMenuItem;
        private ToolStripMenuItem _revealAllMenuItem;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem openToolStripMenuItem;
    }
}
