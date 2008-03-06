using System;
using System.Resources;
using System.Drawing;
using System.Collections;
using System.Windows.Forms;
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

        public InputDialog(string dialogTitle, string prompt, string defaultText)
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.CenterParent;
	    this.Text = dialogTitle;
            Prompt = prompt;
            Input = defaultText;
        }

        public string Input
        {
            get { return inputTextBox.Text; }
            set
            {
                inputTextBox.Text = value;
            }
        }
	
        public string Prompt
        {
            get { return promptLabel.Text; }
            set
            {
                promptLabel.Text = value;
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
            this.promptLabel = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.inputTextBox = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // promptLabel
            // 
            this.promptLabel.Location = new System.Drawing.Point(12, 8);
            this.promptLabel.Name = "promptLabel";
            this.promptLabel.Size = new System.Drawing.Size(240, 48);
            this.promptLabel.TabIndex = 1;
            // 
            // btnOK
            // 
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(16, 104);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(96, 24);
            this.btnOK.TabIndex = 2;
            this.btnOK.Text = "&OK";
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(152, 104);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(96, 24);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "&Cancel";
            // 
            // inputTextBox
            // 
            this.inputTextBox.Location = new System.Drawing.Point(16, 72);
            this.inputTextBox.Name = "inputTextBox";
            this.inputTextBox.Size = new System.Drawing.Size(232, 20);
            this.inputTextBox.TabIndex = 0;
            this.inputTextBox.TextChanged += new System.EventHandler(this.inputTextBox_TextChanged);
            // 
            // InputDialog
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(266, 151);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.promptLabel);
            this.Controls.Add(this.inputTextBox);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "InputDialog";
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion

        protected void btnOK_Click(object sender, System.EventArgs e)
        {
            // inputText = inputTextBox.Text;
        }

        private void inputTextBox_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
