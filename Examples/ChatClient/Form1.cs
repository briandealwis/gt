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
        private const int ChatMessagesChannel = 1;

        /// <summary>
        /// The client repeater uses channel #0 by default to send updates
        /// on clients joining or leaving the group.
        /// </summary>
        private const int SessionUpdatesChannel = 0;

        private Client client;

        /// <summary>
        /// Used to send and receive new chat messages.
        /// </summary>
        private IStringStream chats;

        /// <summary>
        /// Receives session updates from the client repeater
        /// when clients join or leave the group.
        /// </summary>
        private ISessionStream updates;

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
            chats = client.GetStringStream(host, port, ChatMessagesChannel, ChannelDeliveryRequirements.ChatLike);
            updates = client.GetSessionStream(host, port, SessionUpdatesChannel, ChannelDeliveryRequirements.SessionLike);
            client.Start();
            InitializeComponent();
            this.Disposed += Form1_Disposed;
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
