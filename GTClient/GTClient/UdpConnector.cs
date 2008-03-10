using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

namespace GT.Net
{
    public class UdpConnector : IConnector
    {
        protected bool active = false;
        public byte[] ProtocolDescriptor
        {
            get { return ASCIIEncoding.ASCII.GetBytes("GT10"); }
        }

        public void Start() { active = true; }
        public void Stop() { active = false; }
        public bool Active { get { return active; } }
        public void Dispose() { Stop(); }

        public ITransport Connect(string address, string port, Dictionary<string, string> capabilities)
        {
            IPHostEntry he = Dns.GetHostEntry(address);
            IPAddress[] addr = he.AddressList;
            UdpClient client = new UdpClient();
            IPEndPoint endPoint = null;

            //try to connect to the address
            CannotConnectToRemoteException error = null;
            for (int i = 0; i < addr.Length; i++)
            {
                try
                {
                    endPoint = new IPEndPoint(addr[0], Int32.Parse(port));
                    client = new UdpClient();
                    client.DontFragment = true; // FIXME: what are the implications of setting this flag?
                    client.Client.Blocking = false;
                    client.Client.SendTimeout = 1;
                    client.Client.ReceiveTimeout = 1;
                    client.Connect(endPoint);
                    error = null;
                    break;
                }
                catch (Exception e)
                {
                    error = new CannotConnectToRemoteException(e);
                }
            }

            if (error != null) { throw error; }

            // FIXME: a handshake is between two people; we assume that if they don't want
            // to talk to us then they'll close the connection.

            // This is the GT (UDP) protocol 1.0:
            // bytes 0 - 3: the protocol version (ASCII for "GT10")
            // bytes 4 - n: the number of bytes in the capability dictionary (see ByteUtils.EncodeLength)
            // bytes n+1 - end: the capability dictionary
            MemoryStream ms = new MemoryStream(4 + 60); // approx: 4 bytes for protocol, 50 for capabilities
            Debug.Assert(ProtocolDescriptor.Length == 4);
            ms.Write(ProtocolDescriptor, 0, 4);
            ByteUtils.EncodeLength(ByteUtils.EncodedDictionaryByteCount(capabilities), ms);
            ByteUtils.EncodeDictionary(capabilities, ms);
            client.Client.Send(ms.GetBuffer(), 0, (int)ms.Length, SocketFlags.None);

            Console.WriteLine("Address resolved and contacted.  Now connected to " + endPoint.ToString());
            return new UdpClientTransport(client);
        }

    }
}
