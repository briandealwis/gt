using System;
using System.Collections.Generic;
using System.Text;
using GTServer;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace ClientRepeater
{
    class Program
    {
        static Server s;

        static void Main(string[] args)
        {
            s = new Server(9999, 1);
            s.MessageReceived += new MessageHandler(s_MessageReceived2);
            s.ClientsJoined += new ClientsJoinedHandler(s_ClientsJoined);
            s.ClientsRemoved += new ClientsRemovedHandler(s_ClientsRemoved);
            //s.ErrorEvent += new ErrorClientHandler(s_ErrorEvent);
            s.StartListening();
        }

        static void s_ErrorEvent(Exception e, System.Net.Sockets.SocketError se, Server.Client c, string explanation)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        static void s_ClientsJoined(List<Server.Client> list)
        {
            foreach (Server.Client client in list)
            {
                //kill client
                int clientId = client.UniqueIdentity;

                //inform others client is gone
                foreach (Server.Client c in s.ClientList)
                {
                    c.Send(clientId, SessionAction.Joined, (byte)0);
                }
            }
        }

        static void s_ClientsRemoved(List<Server.Client> list)
        {
            foreach (Server.Client client in list)
            {
                //kill client
                int clientId = client.UniqueIdentity;
                Server.Client.Kill(client);

                //inform others client is gone
                foreach (Server.Client c in s.ClientList)
                {
                    c.Send(clientId, SessionAction.Left, (byte)0);
                }
            }
        }

        static void s_MessageReceived(byte id, MessageType messageType, byte[] data, Server.Client client, MessageProtocol protocol)
        {
            //repeat whatever we receive to everyone else
            List<MessageOut> list = new List<MessageOut>();
            list.Add(new MessageOut((byte)id, messageType, data));
            s.Send(list, s.ClientList, protocol);
        }

        static void s_MessageReceived2(byte id, MessageType messageType, byte[] data, Server.Client client, MessageProtocol protocol)
        {
            //repeat whatever we receive to everyone else
            foreach (Server.Client c in s.ClientList)
            {
                c.Send(data, id, messageType, protocol);
            }
        }
    }
}
