using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Telepointers
{
    public partial class Form2 : Form
    {
        public string Result = "127.0.0.1";

        public Form2()
        {
            InitializeComponent();
            textBox1.Text = Result;
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                Result = textBox1.Text;
                this.DialogResult = DialogResult.OK;
            }
        }
    }
}