using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using GT.Utils;

namespace GT.Net
{

    /// <summary>
    /// Class responsible for connecting (and negotiating) a connection to a
    /// remote server using UDP.
    /// </summary>
    /// <remarks>
    /// The use of <see cref="TransportFactory{T}"/> may seem to be a bit complicated,
    /// but it greatly simplifies testing.
    /// </remarks>
    public class UdpConnector : IConnector
    {
        protected bool active = false;
        protected TransportFactory<UdpClient> factory;

        /// <summary>
        /// The default constructor is for an unordered, unsequenced UDP
        /// </summary>
        public UdpConnector() : this(Ordering.Unordered) {}

        public UdpConnector(Ordering ordering)
        {
            switch (ordering)
            {
                case Ordering.Unordered:
                    factory = new TransportFactory<UdpClient>(
                        BaseUdpTransport.UnorderedProtocolDescriptor,
                        h => new UdpClientTransport(h),
                        t => t is UdpClientTransport);
                    return;
                case Ordering.Sequenced:
                    factory = new TransportFactory<UdpClient>(
                        BaseUdpTransport.SequencedProtocolDescriptor,
                        h => new UdpSequencedClientTransport(h),
                        t => t is UdpSequencedClientTransport);
                    return;
                default: throw new InvalidOperationException("Unsupported ordering type: " + ordering);
            }
        }

        public UdpConnector(TransportFactory<UdpClient> factory)
        {
            this.factory = factory;
        }

        public byte[] ProtocolDescriptor
        {
            get { return factory.ProtocolDescriptor; }
        }

        public void Start() { active = true; }
        public void Stop() { active = false; }
        public bool Active { get { return active; } }
        public void Dispose() { Stop(); }

        public ITransport Connect(string address, string port, IDictionary<string, string> capabilities)
        {
            IPAddress[] addr = Dns.GetHostAddresses(address);
            UdpClient client = new UdpClient();
            IPEndPoint endPoint = null;

            //try to connect to the address
            CannotConnectException error = null;
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
                    error = new CannotConnectException(String.Format("Cannot connect to {0}/{1}: {2}",
                        address, port, e.Message), e);
                    error.SourceComponent = this;
                }
            }

            if (error != null) { throw error; }

            // FIXME: a handshake is between two people; we assume that if they don't want
            // to talk to us then they'll close the connexion.

            // This is the GT (UDP) protocol 1.0:
            // bytes 0 - 3: the protocol version (the result from ProtocolDescriptor)
            // bytes 4 - n: the number of bytes in the capability dictionary (see ByteUtils.EncodeLength)
            // bytes n+1 - end: the capability dictionary
            MemoryStream ms = new MemoryStream(4 + 60); // approx: 4 bytes for protocol, 50 for capabilities
            Debug.Assert(ProtocolDescriptor.Length == 4);
            ms.Write(ProtocolDescriptor, 0, 4);
            ByteUtils.EncodeLength(ByteUtils.EncodedDictionaryByteCount(capabilities), ms);
            ByteUtils.EncodeDictionary(capabilities, ms);
            client.Client.Send(ms.GetBuffer(), 0, (int)ms.Length, SocketFlags.None);

            Console.WriteLine("Now connected to UDP: " + endPoint);
            return factory.CreateTransport(client);
        }

        public bool Responsible(ITransport transport)
        {
            return factory.Responsible(transport);
        }


    }
}
