using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GT.Net;

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
        private const int TelepointersChannel = 1;

        /// <summary>
        /// The client repeater uses channel #0 by default to send updates
        /// on clients joining or leaving the group.
        /// </summary>
        private const int SessionUpdatesChannel = 0;

        private Client client;

        /// <summary>
        /// Used to send and receive updated telepointer coordinates.
        /// </summary>
        private IStreamedTuple<int, int> coords;

        /// <summary>
        /// Receives session updates from the client repeater
        /// when clients join or leave the group.
        /// </summary>
        private ISessionStream updates;

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
            Form2 f = new Form2();
            f.ShowDialog();

            this.Paint += Form1_Paint;
            this.Disposed += Form1_Disposed;
            this.MouseMove += Form1_MouseMoved;

            InitializeComponent();

            this.SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            this.SetStyle(ControlStyles.DoubleBuffer, true);

            // Set up GT
            client = new Client(new DefaultClientConfiguration());
            client.ErrorEvent += es => Console.WriteLine(es);
            client.Start();

            updates = client.GetSessionStream(f.Result, "9999", SessionUpdatesChannel, 
                ChannelDeliveryRequirements.SessionLike);
            updates.MessagesReceived += updates_SessionMessagesReceived;

            coords = client.GetStreamedTuple<int, int>(f.Result, "9999", TelepointersChannel, 
                TimeSpan.FromMilliseconds(50), 
                ChannelDeliveryRequirements.TelepointerLike);
            coords.StreamedTupleReceived += coords_StreamedTupleReceived;
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
        /// <param name="stream"></param>
        private void updates_SessionMessagesReceived(ISessionStream stream)
        {
            SessionMessage m;
            while ((m = stream.DequeueMessage(0)) != null)
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
                Color tpc = clientId == coords.Identity ? Color.Pink : Color.Blue;
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

        private void Form1_Disposed(object sender, EventArgs e)
        {
            client.Stop();
            client.Dispose();
        }

        private void timerRepaint_Tick(object sender, EventArgs e)
        {
            client.Update();
            Redraw();
        }
    }
}
