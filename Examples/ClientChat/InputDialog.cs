using System;
using System.Resources;
using System.Drawing;
using System.Collections;
using System.Windows.Forms;
using System.Resources;
using System.ComponentModel;

namespace ClientChat
{
    public class InputDialog : Form
    {
        protected Container components;
        protected Button btnOK;
        protected Button btnCancel;
        protected Label promptLabel;
        protected TextBox inputTextBox;

        protected string dialogTitle;
        protected string promptText;
        protected string inputText;

        public InputDialog(string dialogTitle, string prompt, string defaultText)
        {
            this.promptText = prompt;
            this.inputText = defaultText;
            InitializeComponent();
            this.StartPosition = FormStartPosition.CenterParent;
        }

        public string Input
        {
            get { return inputText; }
            set
            {
                inputText = value;
                inputTextBox.Text = inputText;
            }
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }


        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.promptLabel = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.inputTextBox = new System.Windows.Forms.TextBox();
            promptLabel.Location = new System.Drawing.Point(12, 8);
            promptLabel.Text = promptText;
            promptLabel.Size = new System.Drawing.Size(240, 48);
            promptLabel.TabIndex = 1;

            btnOK.Location = new System.Drawing.Point(16, 104);
            btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            btnOK.Size = new System.Drawing.Size(96, 24);
            btnOK.TabIndex = 2;
            btnOK.Text = "OK";
            btnOK.Click += new System.EventHandler(this.btnOK_Click);
            btnCancel.Location = new System.Drawing.Point(152, 104);
            btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnCancel.Size = new System.Drawing.Size(96, 24);
            btnCancel.TabIndex = 3;
            btnCancel.Text = "Cancel";
            inputTextBox.Location = new System.Drawing.Point(16, 72);
            inputTextBox.TabIndex = 0;
            inputTextBox.Size = new System.Drawing.Size(232, 20);
            inputTextBox.Text = inputText;
            this.Text = dialogTitle;
            this.MaximizeBox = false;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.ControlBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new System.Drawing.Size(266, 151);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.promptLabel);
            this.Controls.Add(this.inputTextBox);
        }
        #endregion

        protected void btnOK_Click(object sender, System.EventArgs e)
        {
            // OK button clicked.
            // get new message.
            inputText = inputTextBox.Text;
        }
    }
}