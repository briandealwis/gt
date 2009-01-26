using System;
using System.IO;
using System.Windows.Forms;

namespace GT.Millipede
{
    public partial class MillipedeConfiguration : Form
    {
        protected MillipedeRecorder recorder;

        /// <summary>
        /// Check and configure the provided recorder, if not yet configured.
        /// </summary>
        /// <param name="recorder">the recorder to possibly configure</param>
        /// <returns>the recorder instance</returns>
        public static MillipedeRecorder Configure(MillipedeRecorder recorder)
        {
            if (recorder.Mode == MillipedeMode.Unconfigured)
            {
                MillipedeConfiguration dialog = new MillipedeConfiguration(recorder);
                dialog.ShowDialog();
            }
            return recorder;
        }

        public MillipedeConfiguration(MillipedeRecorder recorder)
        {
            InitializeComponent();
            this.recorder = recorder;
            rbPlayback.Checked = true;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (rbPlayback.Checked)
            {
                btnBrowse.Enabled = false;
                fileTextBox.Enabled = false;
                btnOk.Enabled = true;
            }
            else if (rbRecord.Checked)
            {
                btnBrowse.Enabled = true;
                fileTextBox.Enabled = true;
                btnOk.Enabled = fileTextBox.Text.Trim().Length > 0;
            }
            else if (rbPlayback.Checked)
            {
                btnBrowse.Enabled = true;
                fileTextBox.Enabled = true;
                btnOk.Enabled = fileTextBox.Text.Trim().Length > 0 &&
                    File.Exists(fileTextBox.Text);
            }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            if (rbPlayback.Checked)
            {
                OpenFileDialog fd = new OpenFileDialog();
                fd.CheckFileExists = true;
                fd.DereferenceLinks = true;
                if (recorder.LastFileName != null)
                {
                    fd.FileName = recorder.LastFileName;
                }
                fd.Multiselect = false;
                if (fd.ShowDialog() == DialogResult.OK)
                {
                    fileTextBox.Text = fd.FileName;
                }
            }
            else if (rbRecord.Checked)
            {
                SaveFileDialog fd = new SaveFileDialog();
                fd.DereferenceLinks = true;
                if (recorder.LastFileName != null)
                {
                    fd.FileName = recorder.LastFileName;
                }
                if (fd.ShowDialog() == DialogResult.OK)
                {
                    fileTextBox.Text = fd.FileName;
                }
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (rbPlayback.Checked)
            {
                recorder.StartReplaying(fileTextBox.Text);
                DialogResult = DialogResult.OK;
            }
            else if (rbRecord.Checked)
            {
                if (File.Exists(fileTextBox.Text))
                {
                    DialogResult result =
                        MessageBox.Show(String.Format("Overwrite {0}?", fileTextBox.Text),
                            "GT Millipede Recorder", MessageBoxButtons.OKCancel);
                    if(result != DialogResult.OK) { return; }
                }
                recorder.StartRecording(fileTextBox.Text);
                DialogResult = DialogResult.OK;
            }
            else if (rbPassthrough.Checked)
            {
                DialogResult = DialogResult.OK;
            }
        }

        private void rbPassthrough_CheckedChanged(object sender, EventArgs e)
        {
            UpdateUI();
        }
    }
}
