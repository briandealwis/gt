using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GT.Net;
using GT.UI;

namespace Telepointers
{
    /// <summary>
    /// A simple demonstration of using a streamed tuple to communicate
    /// telepointer presence messages.  The local mouse pointers is propagated
    /// automatically on a periodic basis.
    /// </summary>
    public partial class Form1 : Form
    {
        /// <summary>
        /// Use channel #1 for sending and receiving telepointer updates
        /// </summary>
        private const int TelepointersChannelId = 1;

        /// <summary>
        /// The client repeater uses channel #0 by default to send updates
        /// on clients joining or leaving the group.
        /// </summary>
        private const int SessionUpdatesChannelId = 0;

        private Client client;

        /// <summary>
        /// Used to send and receive updated telepointer coordinates.
        /// </summary>
        private IStreamedTuple<int, int> coords;

        /// <summary>
        /// Receives session updates from the client repeater
        /// when clients join or leave the group.
        /// </summary>
        private ISessionChannel updates;

        /// <summary>
        /// The list of current clients and their respective telepointers.
        /// </summary>
        private Dictionary<int, Telepointer> telepointers = new Dictionary<int, Telepointer>();

        /// <summary>
        /// A record for a particular telepointer.
        /// </summary>
        private class Telepointer
        {
            float x = 0, y = 0;
            readonly Color color;

            public Telepointer(Color color)
            {
                this.color = color;
            }

            public void Draw(Graphics g)
            {
                g.DrawRectangle(new Pen(color), x, y, 5, 5);
            }

            public void Update(float newX, float newY)
            {
                this.x = newX;
                this.y = newY;
            }
        }

        public Form1()
        {
            InitializeComponent();

            this.SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            this.SetStyle(ControlStyles.DoubleBuffer, true);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            InputDialog d = new InputDialog("Connection details", "Which server:port ?", "localhost:9999");
            if (d.ShowDialog() != DialogResult.OK)
            {
                throw new InvalidOperationException();
            }
            string[] parts = d.Input.Split(':');
            string host = parts[0];
            string port = parts.Length > 1 ? parts[1] : "9999";


            // Set up GT
            client = new Client(new DefaultClientConfiguration());
            client.ErrorEvent += es => Console.WriteLine(es);
            client.ConnexionRemoved += client_ConnexionRemoved;
            client.Start();

            updates = client.OpenSessionChannel(host, port, SessionUpdatesChannelId,
                ChannelDeliveryRequirements.SessionLike);
            updates.MessagesReceived += updates_SessionMessagesReceived;

            coords = client.OpenStreamedTuple<int, int>(host, port, TelepointersChannelId,
                TimeSpan.FromMilliseconds(50),
                ChannelDeliveryRequirements.AwarenessLike);
            coords.StreamedTupleReceived += coords_StreamedTupleReceived;
        }

        private void client_ConnexionRemoved(Communicator c, IConnexion conn)
        {
            if(!IsDisposed && client.Connexions.Count == 0)
            {
                MessageBox.Show(this, "Disconnected from server", Text);
                Close();
            }
        }

        /// <summary>
        /// Cause the streamed tuple to be updated on local mouse movement.
        /// This value is only stored locally, and only propagated to the other 
        /// clients every 50 ms as defined by the tuple stream creation in the
        /// <see cref="IStreamedTuple{T_X,T_Y}.UpdatePeriod"/>
        /// <see cref="Form1">constructor</see>.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_MouseMoved(object sender, MouseEventArgs e)
        {
            coords.X = e.X;
            coords.Y = e.Y;
        }

        /// <summary>
        /// Process an update about my fellows
        /// </summary>
        /// <param name="channel"></param>
        private void updates_SessionMessagesReceived(ISessionChannel channel)
        {
            SessionMessage m;
            while ((m = channel.DequeueMessage(0)) != null)
            {
                Console.WriteLine("Session: " + m);
                if (m.Action == SessionAction.Left)
                {
                    telepointers.Remove(m.ClientId);
                    Redraw();
                }
            }
        }

        /// <summary>
        /// Update telepointers for some client (may be my own!)
        /// </summary>
        /// <param name="tuple"></param>
        /// <param name="clientId"></param>
        private void coords_StreamedTupleReceived(RemoteTuple<int, int> tuple, int clientId)
        {
            if (!telepointers.ContainsKey(clientId))
            {
                // Ensure we're different
                Color tpc = clientId == coords.Identity ? Color.Red : Color.Blue;
                telepointers.Add(clientId, new Telepointer(tpc));
            }
            telepointers[clientId].Update(tuple.X, tuple.Y);
        }

        private void Redraw()
        {
            BeginInvoke(new MethodInvoker(Invalidate));
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.SeaShell);
            foreach (int i in telepointers.Keys)
            {
                telepointers[i].Draw(g);
            }
        }

        private void timerRepaint_Tick(object sender, EventArgs e)
        {
            // timer starts on creation; client created only on form load
            if (client != null) { client.Update(); }
            Redraw();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            client.Stop();
            client.Dispose();
        }

    }
}
