using System;
using System.Collections.Generic;
using BBall.Common;
using GT.Net;

namespace BBall.Server
{
    public class BBServer : IDisposable

    {
        public event GT.Utils.Action<double, double> PositionUpdates;

        private GT.Net.Server server;
        private WindowBoundsChanged windowBounds;

        public BBServer(ushort port)
        {
            server = new GT.Net.Server(new DefaultServerConfiguration(port));
            server.ObjectMessageReceived += _server_ObjectReceived;
            server.ClientsJoined += _server_NewClients;
        }

        private void _server_ObjectReceived(Message m, IConnexion client, ITransport transport)
        {
            if (m is ObjectMessage)
            {
                object obj = ((ObjectMessage) m).Object;
                if (obj is PositionChanged)
                {
                    if(PositionUpdates != null)
                    {
                        PositionUpdates(((PositionChanged) obj).X, ((PositionChanged) obj).Y);
                    }
                }
            }
        }

        private void _server_NewClients(ICollection<IConnexion> list)
        {
            if (windowBounds != null)
            {
                foreach (IConnexion cnx in list)
                {
                    cnx.Send(new ObjectMessage(1, windowBounds),
                             null, ChannelDeliveryRequirements.MostStrict);
                }
            }
        }

        public void UpdateWindowSize(uint width, uint height, uint radius)
        {
            windowBounds = new WindowBoundsChanged {Height = height, Width = width, Radius = radius};
            foreach (IConnexion cnx in server.Connexions)
            {
                cnx.Send(new ObjectMessage(1, windowBounds), 
                    null, ChannelDeliveryRequirements.MostStrict);
            }
        }

        public void Start()
        {
            server.Start();
            server.StartSeparateListeningThread();
        }

        public void Dispose()
        {
            server.Dispose();
        }
    }


}