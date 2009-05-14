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

using System.Drawing;
using System.Windows.Forms;

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
