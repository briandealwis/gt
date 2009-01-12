using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using Common.Logging;
using GT.Utils;

namespace GT.Net
{
    public class TcpConnector : IConnector
    {
        protected ILog log;

        protected bool active = false;

        public TcpConnector()
        {
            log = LogManager.GetLogger(GetType());
        }

        public byte[] ProtocolDescriptor
        {
            get { return ASCIIEncoding.ASCII.GetBytes("GT10"); }
        }

        public void Start() { active = true;  }
        public void Stop() { active = false; }
        public bool Active { get { return active; } }
        public void Dispose() { Stop(); }

        public ITransport Connect(string address, string port, IDictionary<string, string> capabilities)
        {
            IPAddress[] addr = Dns.GetHostAddresses(address);
            TcpClient client = null;
            IPEndPoint endPoint = null;

            //try to connect to the address
            CannotConnectException error = null;
            for (int i = 0; i < addr.Length; i++)
            {
                try
                {
                    endPoint = new IPEndPoint(addr[0], Int32.Parse(port));
                    client = new TcpClient();
                    client.NoDelay = true;
                    client.ReceiveTimeout = 1;
                    client.SendTimeout = 1;
                    client.Connect(endPoint);
                    client.Client.Blocking = false;
                    error = null;
                    break;
                }
                catch (Exception e)
                {
                    error = new CannotConnectException(String.Format("Cannot connect to {0}/{1}: {2}",
                        address, port, e.Message), e);
                    error.SourceComponent = this;
                }
            }

            if (error != null) { throw error; }

            // FIXME: a handshake is between two people; we assume that if they don't want
            // to talk to us then they'll close the connexion.

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

            log.Info("Now connected via TCP: " + endPoint);
            return new TcpTransport(client);
        }

        public bool Responsible(ITransport transport)
        {
            return transport is TcpTransport;
        }

    }
}
