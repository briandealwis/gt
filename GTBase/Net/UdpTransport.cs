using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using GT.Utils;

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
        protected Queue<byte[]> outstanding;

        public override Reliability Reliability { get { return Reliability.Unreliable; } }
        public override Ordering Ordering { get { return Ordering.Unordered; } }

        public BaseUdpTransport()
        {
            PacketHeaderSize = 0;   // GT UDP 1.0 doesn't need a packet length
            outstanding = new Queue<byte[]>();
        }

        public override string Name { get { return "UDP"; } }

        public override uint Backlog { get { return (uint)outstanding.Count; } }

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
            ContractViolation.Assert(length <= MaximumPacketSize, String.Format(
                    "Packet exceeds transport capacity: {0} > {1}", length, MaximumPacketSize));

            DebugUtils.DumpMessage(this + "SendPacket", buffer);
            if (offset != 0 || length != buffer.Length)
            {
                // FIXME: should encode an object rather than copying
                byte[] newBuffer = new byte[length];
                Array.Copy(buffer, offset, newBuffer, 0, length);
                buffer = newBuffer;
            }
            lock (this)
            {
                outstanding.Enqueue(buffer);
            }
            FlushOutstandingPackets();
        }


        /// <summary>Send a message to server.</summary>
        /// <param name="buffer">The message to send.</param>
        public override void SendPacket(Stream ms)
        {
            InvalidStateException.Assert(Active, "Cannot send on disposed transport", this);
            ContractViolation.Assert(ms.Length > 0, "Cannot send 0-byte messages!");
            ContractViolation.Assert(ms.Length - PacketHeaderSize <= MaximumPacketSize, String.Format(
                    "Packet exceeds transport capacity: {0} > {1}", ms.Length - PacketHeaderSize, MaximumPacketSize));
            if (!(ms is MemoryStream))
            {
                throw new ArgumentException("Transport provided different stream!");
            }
            MemoryStream output = (MemoryStream)ms;
            lock (this)
            {
                outstanding.Enqueue(output.ToArray());
            }
            FlushOutstandingPackets();
        }

    }
}