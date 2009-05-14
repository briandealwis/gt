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

namespace GT.ChatClient
{
    partial class Form1
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
            this.components = new System.ComponentModel.Container();
            this.timer = new System.Windows.Forms.Timer(this.components);
            this.composedBox = new System.Windows.Forms.TextBox();
            this.transcriptBox = new System.Windows.Forms.RichTextBox();
            this.SuspendLayout();
            // 
            // timer
            // 
            this.timer.Enabled = true;
            this.timer.Tick += new System.EventHandler(this.timer_Tick);
            // 
            // composeBox
            // 
            this.composedBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.composedBox.Location = new System.Drawing.Point(12, 172);
            this.composedBox.Name = "composedBox";
            this.composedBox.Size = new System.Drawing.Size(251, 20);
            this.composedBox.TabIndex = 0;
            this.composedBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.composedBox_KeyDown);
            // 
            // conversationBox
            // 
            this.transcriptBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.transcriptBox.BackColor = System.Drawing.SystemColors.Window;
            this.transcriptBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.transcriptBox.Location = new System.Drawing.Point(12, 12);
            this.transcriptBox.Name = "transcriptBox";
            this.transcriptBox.ReadOnly = true;
            this.transcriptBox.Size = new System.Drawing.Size(251, 157);
            this.transcriptBox.TabIndex = 1;
            this.transcriptBox.Text = "";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(278, 207);
            this.Controls.Add(this.transcriptBox);
            this.Controls.Add(this.composedBox);
            this.Name = "Form1";
            this.Text = "Chat";
            this.Activated += new System.EventHandler(this.Form1_Activated);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Timer timer;
        private System.Windows.Forms.TextBox composedBox;
        private System.Windows.Forms.RichTextBox transcriptBox;
    }
}

