using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AmySonicVisualizer
{
    public partial class ViewerForm : Form
    {
        public ViewerForm()
        {
            InitializeComponent();

            Text = "Amysonic Visualizer";
            ClientSize = new Size(1280, 600);

            // Route form-level keystrokes to the user control
            KeyPreview = true;
            KeyDown += ViewerForm_KeyDown;
        }

        private void ViewerForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                _spectrogramControl.TogglePlayback();
            }
        }

        private void _revealAllMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            _spectrogramControl.RevealAll = _revealAllMenuItem.Checked;
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _spectrogramControl.PromptOpenFile();
        }

        private void viewToolStripMenuItem1_CheckedChanged(object sender, EventArgs e)
        {

        }
    }
}