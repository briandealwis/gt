using System.Diagnostics;
using BBall.Common;
using GT.Net;
using GT.Net.Utils;
using System.Timers;
using System;

namespace BBall.Client
{
    class BBClientConfiguration : DefaultClientConfiguration
    {
        public override ITransport ConfigureTransport(ITransport t)
        {
            if(t is NetworkEmulatorTransport) { return t; }
            return new NetworkEmulatorTransport(t);
        }
    }

    public class BBClient : IDisposable
    {
        private GT.Net.Client client;
        private readonly string host;
        private readonly uint port;
        private Stopwatch sw;
        private Timer timer;
        private IObjectChannel updates;

        private double x = 6;
        private double y = 6;
        private double velX = 50;  // 5 pixels / second
        private double velY = 50;  // 5 pixels / second

        private double radius;
        private double width = 100;
        private double height = 100;


        public BBClient(string host, uint port)
        {
            client = new GT.Net.Client(new BBClientConfiguration());
            this.host = host;
            this.port = port;
            Interval = 10;  // 10 Hz
        }

        // In hertz
        public uint Interval { get; set; }

        public float Delay
        {
            set
            {
                foreach(IConnexion cnx in client.Connexions)
                {
                    foreach (ITransport t in cnx.Transports)
                    {
                        if(t is NetworkEmulatorTransport)
                        {
                            NetworkEmulatorTransport net = (NetworkEmulatorTransport) t;
                            net.Delay = value;
                        }
                    }
                }
            }
        }


        public void Start()
        {
            sw = new Stopwatch();

            client.Start();
            client.StartSeparateListeningThread();

            updates = client.OpenObjectChannel(host, port.ToString(), 1, 
                ChannelDeliveryRequirements.MostStrict);
            updates.MessagesReceived += _updates_Received;

            timer = new Timer(1.0 / (double)Interval);
            timer.AutoReset = true;
            timer.Elapsed += _timer_Tick;
            timer.Start();
            sw.Start();
        }

        private void _updates_Received(IObjectChannel channel)
        {
            object obj;
            while((obj = channel.DequeueMessage(0)) != null)
            {
                if(obj is WindowBoundsChanged)
                {
                    height = ((WindowBoundsChanged) obj).Height;
                    width = ((WindowBoundsChanged)obj).Width;
                    radius = ((WindowBoundsChanged)obj).Radius;
                } else
                {
                    Console.WriteLine("Unknown message: " + obj);
                }
            }
        }

        private void _timer_Tick(object sender, ElapsedEventArgs e)
        {
            UpdatePosition();
            updates.Send(new PositionChanged { X = x, Y = y });   
        }

        private void UpdatePosition()
        {
            sw.Start();
            double elapsed = sw.Elapsed.TotalSeconds;
            x += velX*elapsed;
            y += velY*elapsed;
            if(x < radius)
            {
                velX = Math.Abs(velX);
                x = radius;
            } else if(x > width - radius)
            {
                velX = -Math.Abs(velX);
                x = width - radius;
            }

            if (y < radius)
            {
                velY = Math.Abs(velY);
                y = radius;
            }
            else if (y > height - radius)
            {
                velY = -Math.Abs(velY);
                y = height - radius;
            }
            sw.Reset();
            sw.Start();
        }

        public void Dispose()
        {
            timer.Dispose();
        }
    }

}