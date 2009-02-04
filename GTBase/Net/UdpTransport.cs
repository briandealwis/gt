using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Text;
using GT.Utils;

namespace GT.Net
{
    public abstract class BaseUdpTransport : BaseTransport
    {
        // Ordering.Unordered => GT10 is a historical value and must not change
        public static readonly byte[] UnorderedProtocolDescriptor = Encoding.ASCII.GetBytes("GT10");
        public static readonly byte[] SequencedProtocolDescriptor = Encoding.ASCII.GetBytes("GS10");
        public static readonly byte[] OrderedProtocolDescriptor = Encoding.ASCII.GetBytes("GO10");

        /// <summary>
        /// Allow setting a cap on the maximum UDP message size
        /// as compared to the OS value normally used.
        /// 512 is the historical value supported by GT.
        /// </summary>
        public static uint DefaultMaximumMessageSize = 512;

        /// <summary>
        /// Theoretical maximum of a UDP datagram size as UDP length field is 16 bits.
        /// But some systems apparently cap a datagram at 8K.
        /// </summary>
        public static uint UDP_MAXDGRAMSIZE = 65536;

        //If bits can't be written to the network now, try later
        //We use this so that we don't have to block on the writing to the network
        protected Queue<byte[]> outstanding;

        public override Reliability Reliability { get { return Reliability.Unreliable; } }
        public override Ordering Ordering { get { return Ordering.Unordered; } }

        protected uint maximumPacketSize = DefaultMaximumMessageSize;

        public BaseUdpTransport(uint packetHeaderSize) 
            : base(packetHeaderSize)
        {
            outstanding = new Queue<byte[]>();
        }

        public override string Name { get { return "UDP"; } }

        public override uint Backlog { get { return (uint)outstanding.Count; } }

        /// <summary>
        /// Set the maximum packet size allowed by this transport.
        /// Although UDP theoretically allows datagrams up to 64K in length
        /// (since its length field is 16 bits), some systems apparently
        /// cap a datagram at 8K.  So the general recommendation
        /// is to use 8K or less.
        /// </summary>
        public override uint MaximumPacketSize
        {
            get { return maximumPacketSize; }
            set
            {
                // UDP packets 
                maximumPacketSize = Math.Min(UDP_MAXDGRAMSIZE, Math.Max(PacketHeaderSize, value));
            }
        }

        override public void Update()
        {
            InvalidStateException.Assert(Active, "Cannot send on disposed transport", this);
            CheckIncomingPackets();
            FlushOutstandingPackets();
        }

        protected abstract void FlushOutstandingPackets();

        protected abstract void CheckIncomingPackets();

        public override void SendPacket(byte[] buffer, int offset, int length)
        {
            InvalidStateException.Assert(Active, "Cannot send on disposed transport", this);
            ContractViolation.Assert(length > 0, "Cannot send 0-byte messages!");
            ContractViolation.Assert(length - PacketHeaderSize <= MaximumPacketSize,
                String.Format("Packet exceeds transport capacity: {0} > {1}",
                    length - PacketHeaderSize, MaximumPacketSize));

            if (PacketHeaderSize > 0 || offset != 0 || length != buffer.Length)
            {
                // FIXME: should encode an object rather than copying
                byte[] newBuffer = new byte[PacketHeaderSize + length];
                Array.Copy(buffer, offset, newBuffer, PacketHeaderSize, length);
                buffer = newBuffer;
            }
            WritePacketHeader(buffer, (uint)length);
            lock (this)
            {
                outstanding.Enqueue(buffer);
            }
            FlushOutstandingPackets();
        }


        /// <summary>Send a message to server.</summary>
        /// <param name="ms">The message to send.</param>
        public override void SendPacket(Stream ms)
        {
            InvalidStateException.Assert(Active, "Cannot send on disposed transport", this);
            ContractViolation.Assert(ms.Length > 0, "Cannot send 0-byte messages!");
            ContractViolation.Assert(ms.Length - PacketHeaderSize <= MaximumPacketSize, 
                String.Format("Packet exceeds transport capacity: {0} > {1}", 
                    ms.Length - PacketHeaderSize, MaximumPacketSize));
            CheckValidPacketStream(ms);

            // we inherit GetPacketStream() which uses a MemoryStream, and the typing
            // is checked by CheckValidStream()
            MemoryStream output = (MemoryStream)ms;
            byte[] buffer = output.ToArray();
            WritePacketHeader(buffer, (uint)(ms.Length - PacketHeaderSize));
            lock (this)
            {
                outstanding.Enqueue(buffer);
            }
            FlushOutstandingPackets();
        }

        /// <summary>
        /// Provide an opportunity to subclasses to write out the packet header
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="packetLength"></param>
        protected virtual void WritePacketHeader(byte[] buffer, uint packetLength)
        {
            // do nothing
        }
    }
}
