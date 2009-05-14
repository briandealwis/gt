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
using System.Windows.Forms;
using GT.Net;
using GT.UI;

namespace GT.ChatClient
{
    public partial class Form1 : Form
    {
        /// <summary>
        /// Use channel #1 for sending and receiving chat messages
        /// </summary>
        private const int ChatMessagesChannelId = 1;

        /// <summary>
        /// The client repeater uses channel #0 by default to send updates
        /// on clients joining or leaving the group.
        /// </summary>
        private const int SessionUpdatesChannelId = 0;

        private Client client;

        /// <summary>
        /// Used to send and receive new chat messages.
        /// </summary>
        private IStringChannel chats;

        /// <summary>
        /// Receives session updates from the client repeater
        /// when clients join or leave the group.
        /// </summary>
        private ISessionChannel updates;

        public Form1()
        {
            InputDialog d = new InputDialog("Connection details", "Which server:port ?", "localhost:9999");
            if (d.ShowDialog() != DialogResult.OK)
            {
                throw new InvalidOperationException();
            }
            string[] parts = d.Input.Split(':');
            string host = parts[0];
            string port = parts.Length > 1 ? parts[1] : "9999";

            client = new Client();
            client.ConnexionRemoved += client_ConnexionRemoved;
            client.Start();
            chats = client.OpenStringChannel(host, port, ChatMessagesChannelId, ChannelDeliveryRequirements.ChatLike);
            updates = client.OpenSessionChannel(host, port, SessionUpdatesChannelId, ChannelDeliveryRequirements.SessionLike);
            InitializeComponent();
            this.Disposed += Form1_Disposed;
        }

        private void client_ConnexionRemoved(Communicator c, IConnexion conn)
        {
            if(!IsDisposed && client.Connexions.Count == 0)
            {
                MessageBox.Show(this, "Disconnected from server", Text);
                Close();
            }
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            client.Update();

            String str;
            while((str = chats.DequeueMessage(0)) != null)
                transcriptBox.Text += str + "\n";

            SessionMessage mes;
            while ((mes = updates.DequeueMessage(0)) != null)
                transcriptBox.Text += "Client " + mes.ClientId + " " + mes.Action + "\n";
        }

        private void composedBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                chats.Send(composedBox.Text);
                composedBox.Text = "";
                e.Handled = true;
            }
        }

        private void Form1_Disposed(object sender, EventArgs e)
        {
            client.Stop();
            client.Dispose();
        }

        private void Form1_Activated(object sender, EventArgs e)
        {
            composedBox.Focus();
        }


    }
}
