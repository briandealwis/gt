using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using GTClient;

namespace PassiveTelepointers
{
    public partial class Form1 : Form
    {
        Graphics g;
        Dictionary<int, Telepointer> teleList = new Dictionary<int, Telepointer>();
        Client c = new Client();
        BinaryStream binary;
        SessionStream session;

        List<Control> controls = new List<Control>();

        private class Telepointer
        {
            float vx = 0, vy = 0, x = 0, y = 0;
            Color color;

            public Telepointer(Color color)
            {
                this.color = color;
            }

            public void Update()
            {
                x += vx;
                y += vy;
            }

            public void Draw(Graphics g)
            {
                g.DrawRectangle(new Pen(color), x, y, 5, 5);
            }

            public void Update(float x, float y, float vx, float vy)
            {
                this.x = x;
                this.y = y;
                this.vx = vx;
                this.vy = vy;
            }
        }

        public Form1()
        {
            Form2 f = new Form2();
            f.ShowDialog();

            this.ControlAdded += new ControlEventHandler(Form1_ControlAdded);
            this.Paint += new PaintEventHandler(Form1_Paint);

            InitializeComponent();

            g = this.CreateGraphics();
            this.SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            this.SetStyle(ControlStyles.DoubleBuffer, true);

            teleList.Add(0,new Telepointer(Color.Pink));

            binary = c.GetBinaryStream(f.Result, "9999", 0);
            session = c.GetSessionStream(f.Result, "9999", 0);
        }

        void Form1_Paint(object sender, PaintEventArgs e)
        {
            Control c = (Control)sender;
            PaintWindow(c.CreateGraphics());
        }

        void Form1_ControlAdded(object sender, ControlEventArgs e)
        {
            Control c = (Control)sender;
            c.Paint += new PaintEventHandler(c_Paint);
            controls.Add(c);
        }

        void c_Paint(object sender, PaintEventArgs e)
        {
            Control c = (Control)sender;
            PaintWindow(c.CreateGraphics());
        }


        public void PaintWindow(Graphics g)
        {
            g.Clear(Color.SeaShell);

            foreach (int i in teleList.Keys)
            {
                if (i == 0)
                {
                    Point loc = this.Location;
                    Point mouse = Form1.MousePosition;
                    teleList[0].Update(mouse.X - loc.X - 10, mouse.Y - loc.Y - 30, 0, 0);
                }
                teleList[i].Draw(g);
            }
        }

        private void timerRepaint_Tick(object sender, EventArgs e)
        {
            byte[] b = new byte[20];
            SessionMessage ses;

            c.Update();

            Point loc = this.Location;
            Point mouse = Form1.MousePosition;

            while ((b = binary.DequeueMessage(0)) != null)
            {
                int id = BitConverter.ToInt32(b, 0);
                float x = BitConverter.ToSingle(b, 4);
                float y = BitConverter.ToSingle(b, 8);
                float vx = BitConverter.ToSingle(b, 12);
                float vy = BitConverter.ToSingle(b, 16);

                if (!teleList.ContainsKey(id))
                {
                    teleList.Add(id, new Telepointer(Color.Blue));
                }
                teleList[id].Update(x, y, vx, vy);
            }

            while ((ses = session.DequeueMessage(0)) != null)
            {
                if (teleList.ContainsKey(ses.ClientId) && ses.Action == SessionAction.Left)
                {
                    teleList.Remove(ses.ClientId);
                }
            }

            this.RaisePaintEvent(null, null);
        }
    }
}