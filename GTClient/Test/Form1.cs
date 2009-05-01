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

namespace Test
{
    public partial class Form1 : Form
    {
        Client client = new Client();
        StringStream stream;

        int sent;
        int received;

        public Form1()
        {
            InitializeComponent();
            stream = client.GetStringStream("127.0.0.1", "9999", 0);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            while (true)
            {
                string test = "";
                for (int i = 0; i < 9999; i++)
                    test += "test";
                stream.Send(test);
                sent++;
                client.Update();
                while (stream.DequeueMessage(0) != null)
                    received++;
            }
        }
    }
}
