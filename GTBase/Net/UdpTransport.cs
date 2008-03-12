using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

namespace GT.Net
{
    public abstract class BaseUdpTransport : BaseTransport
    {
        /// <summary>
        /// Allow setting a cap on the maximum UDP message size
        /// as compared to the OS value normally used.
        /// 512 is the historical value supported by GT.
        /// </summary>
        public static int CappedMessageSize = 512;

        //If bits can't be written to the network now, try later
        //We use this so that we don't have to block on the writing to the network
        protected List<byte[]> outstanding = new List<byte[]>();

        // FIXME: Stop-gap measure until we have QoS descriptors
        public override MessageProtocol MessageProtocol { get { return MessageProtocol.Udp; } }

        public BaseUdpTransport()
        {
            PacketHeaderSize = 0;   // GT UDP 1.0 doesn't need a packet length
        }

        public override string Name { get { return "UDP"; } }

        override public void Update()
        {
            if (!Active) { throw new InvalidStateException("Cannot send on disposed transport"); }
            CheckIncomingPackets();
            FlushOutstandingPackets();
        }

        protected abstract void FlushOutstandingPackets();

        protected abstract void CheckIncomingPackets();

        public override void SendPacket(byte[] buffer, int offset, int length)
        {
            if (!Active) { throw new InvalidStateException("Cannot send on disposed transport"); }
            DebugUtils.DumpMessage(this + "SendPacket", buffer);
            if (offset != 0 || length != buffer.Length)
            {
                byte[] newBuffer = new byte[length];
                Array.Copy(buffer, offset, newBuffer, 0, length);
                buffer = newBuffer;
            }
            outstanding.Add(buffer);
            FlushOutstandingPackets();
        }


        /// <summary>Send a message to server.</summary>
        /// <param name="buffer">The message to send.</param>
        public override void SendPacket(Stream ms)
        {
            if (!Active) { throw new InvalidStateException("Cannot send on disposed transport"); }
            if (!(ms is MemoryStream))
            {
                throw new ArgumentException("Transport provided different stream!");
            }
            MemoryStream output = (MemoryStream)ms;
            outstanding.Add(output.ToArray());
            FlushOutstandingPackets();
        }

    }
}