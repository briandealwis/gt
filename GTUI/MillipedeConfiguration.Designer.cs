//
// GT: The Groupware Toolkit for C#
// Copyright (C) 2006 - 2009 by the University of Saskatchewan
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later
// version.
// 
// This library is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA
// 02110-1301  USA
// 

namespace GT.Millipede
{
    partial class MillipedeConfiguration
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.rbPassthrough = new System.Windows.Forms.RadioButton();
            this.rbRecord = new System.Windows.Forms.RadioButton();
            this.rbPlayback = new System.Windows.Forms.RadioButton();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.fileTextBox = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // rbPassthrough
            // 
            this.rbPassthrough.AutoSize = true;
            this.rbPassthrough.Checked = true;
            this.rbPassthrough.Location = new System.Drawing.Point(36, 62);
            this.rbPassthrough.Name = "rbPassthrough";
            this.rbPassthrough.Size = new System.Drawing.Size(297, 17);
            this.rbPassthrough.TabIndex = 0;
            this.rbPassthrough.TabStop = true;
            this.rbPassthrough.Text = "&Skip Millipede and allow the application to run unmolested";
            this.rbPassthrough.UseVisualStyleBackColor = true;
            this.rbPassthrough.CheckedChanged += new System.EventHandler(this.rbPassthrough_CheckedChanged);
            // 
            // rbRecord
            // 
            this.rbRecord.AutoSize = true;
            this.rbRecord.Location = new System.Drawing.Point(36, 85);
            this.rbRecord.Name = "rbRecord";
            this.rbRecord.Size = new System.Drawing.Size(294, 17);
            this.rbRecord.TabIndex = 1;
            this.rbRecord.Text = "&Record this application\'s network traffic for later playback";
            this.rbRecord.UseVisualStyleBackColor = true;
            this.rbRecord.CheckedChanged += new System.EventHandler(this.rbPassthrough_CheckedChanged);
            // 
            // rbPlayback
            // 
            this.rbPlayback.AutoSize = true;
            this.rbPlayback.Location = new System.Drawing.Point(36, 108);
            this.rbPlayback.Name = "rbPlayback";
            this.rbPlayback.Size = new System.Drawing.Size(234, 17);
            this.rbPlayback.TabIndex = 2;
            this.rbPlayback.Text = "&Playback previously recorded network traffic";
            this.rbPlayback.UseVisualStyleBackColor = true;
            this.rbPlayback.CheckedChanged += new System.EventHandler(this.rbPassthrough_CheckedChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(51, 30);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(252, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Please select the Millipede mode for this application:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(18, 153);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(44, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "Source:";
            // 
            // fileTextBox
            // 
            this.fileTextBox.Location = new System.Drawing.Point(68, 150);
            this.fileTextBox.Name = "fileTextBox";
            this.fileTextBox.Size = new System.Drawing.Size(191, 20);
            this.fileTextBox.TabIndex = 5;
            // 
            // btnBrowse
            // 
            this.btnBrowse.Location = new System.Drawing.Point(267, 149);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(68, 23);
            this.btnBrowse.TabIndex = 6;
            this.btnBrowse.Text = "Browse...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // btnOk
            // 
            this.btnOk.Location = new System.Drawing.Point(153, 196);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(71, 22);
            this.btnOk.TabIndex = 7;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // MillipedeConfiguration
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(378, 240);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.fileTextBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.rbPlayback);
            this.Controls.Add(this.rbRecord);
            this.Controls.Add(this.rbPassthrough);
            this.Name = "MillipedeConfiguration";
            this.Text = "GT Millipede Configuration";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RadioButton rbPassthrough;
        private System.Windows.Forms.RadioButton rbRecord;
        private System.Windows.Forms.RadioButton rbPlayback;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox fileTextBox;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Button btnOk;
    }
}
