using System;
using System.Collections.Generic;
using System.Text;
using GT.Net;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Net.Sockets;
using GT.Net;

namespace ClientRepeater
{
    class Program
    {
        static Server s;

        static void Main(string[] args)
        {
            int port = 9999;
            if (args.Length > 1)
            {
                Console.WriteLine("Use: <ClientRepeater.exe> [port]");
                Console.WriteLine("[port] defaults to {0} if not specified", port);
                return;
            }
            else if (args.Length == 1)
            {
                port = Int32.Parse(args[0]);
                if (port <= 0)
                {
                    Console.WriteLine("error: port must be greater than 0");
                    return;
                }
            }
            s = new Server(port, 1);
            s.MessageReceived += new MessageHandler(s_MessageReceived);
            s.ClientsJoined += new ClientsJoinedHandler(s_ClientsJoined);
            s.ClientsRemoved += new ClientsRemovedHandler(s_ClientsRemoved);
            s.ErrorEvent += new ErrorClientHandler(s_ErrorEvent);
            s.StartListening();
        }

        static void s_ErrorEvent(Exception e, SocketError se, Server.Client c, string explanation)
        {
            Console.WriteLine(DateTime.Now + " Error[" + c + "]: " + explanation + " (" + e + ", " + se + ")");
            if (se.Equals(SocketError.NoRecovery))  // FIXME: this test is bogus -- we need more information
            {
                List<Server.Client> removed = new List<Server.Client>();
                removed.Add(c);
                s_ClientsRemoved(removed);
            }
        }

        static void s_ClientsJoined(ICollection<Server.Client> list)
        {
            foreach (Server.Client client in list)
            {
                int clientId = client.UniqueIdentity;
                Console.WriteLine(DateTime.Now + " Joined: " + clientId + " (" + client + ")");

                foreach (Server.Client c in s.Clients)
                {
                        c.Send(clientId, SessionAction.Joined, (byte)0, MessageProtocol.Tcp);
                }
            }
        }

        static void s_ClientsRemoved(ICollection<Server.Client> list)
        {
            foreach (Server.Client client in list)
            {
                //kill client
                int clientId = client.UniqueIdentity;
                Console.WriteLine(DateTime.Now + " Left: " + clientId + " (" + client + ")");
                try
                {
                    client.Dispose();
                } catch(Exception e) {
                    Console.WriteLine("{0} EXCEPTION: when stopping client {1} id#{2}: {3}",
                        DateTime.Now, clientId, client, e);
                }

                //inform others client is gone
                foreach (Server.Client c in s.Clients)
                {
                    c.Send(clientId, SessionAction.Left, (byte)0, MessageProtocol.Tcp);
                }
            }
        }

        static void s_MessageReceived(Message m, Server.Client client, 
            MessageProtocol protocol)
        {
            //repeat whatever we receive to everyone else
            s.Send(m, s.Clients, protocol);
        }
    }
}
