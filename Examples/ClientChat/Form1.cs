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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using GT.Net;
using GT.Net;

namespace ClientChat
{
    public partial class Form1 : Form
    {
        private Client c;
        private IStringStream s;
        private ISessionStream session;

        public Form1()
        {
            c = new Client();
            InputDialog d = new InputDialog("Connection details", "Which server:port ?", "localhost:9999");
            if (d.ShowDialog() != DialogResult.OK)
            {
                throw new InvalidOperationException();
            }
            string[] parts = d.Input.Split(':');
            string host = parts[0];
            string port = parts.Length > 1 ? parts[1] : "9999";

            s = c.GetStringStream(host, port,0, ChannelDeliveryRequirements.ChatLike);
            session = c.GetSessionStream(host, port, 0, ChannelDeliveryRequirements.SessionLike);
            InitializeComponent();
            this.Disposed += Form1_Disposed;
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            c.Update();
            String str;
            SessionMessage mes;

            while((str = s.DequeueMessage(0)) != null)
                richTextBox.Text += str + "\n";

            while ((mes = session.DequeueMessage(0)) != null)
                richTextBox.Text += "Client " + mes.ClientId + " " + mes.Action + "\n";
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (e.KeyCode == Keys.Enter)
            {
                s.Send(textBox1.Text);
                textBox1.Text = "";
            }
        }

        void Form1_Disposed(object sender, EventArgs e)
        {
            c.Stop();
            c.Dispose();
        }


    }
}
