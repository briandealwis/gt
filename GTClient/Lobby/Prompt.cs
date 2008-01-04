using System;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace Lobby
{
    public class Prompt
    {
        public static string Show(string question)
        {
            Form form = new Form();
            Label label = new Label();
            TextBox textBox = new TextBox();
            int lengthNeeded = (int)(question.Length*label.Font.SizeInPoints);

            
            form.TopMost = true;
            form.ClientSize = new Size(lengthNeeded+10, 50);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;

            label.Text = question;
            label.Location = new Point(5, 0);
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Size = new Size(lengthNeeded, 23);


            textBox.Location = new Point(5, 25);
            textBox.Size = new Size(lengthNeeded, 23);
            textBox.KeyDown += new KeyEventHandler(KeyDown);

            form.Controls.Add(label);
            form.Controls.Add(textBox);

            if (form.ShowDialog() == DialogResult.OK)
                return textBox.Text;
            else
                return null;
        }

        static void KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyData == Keys.Enter)
                ((TextBox)sender).FindForm().DialogResult = DialogResult.OK;
        }
    }
}
