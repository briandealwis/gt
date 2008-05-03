using System;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using GT.Net;
using System.IO;
using System.Diagnostics;
using System.Text;
using GT.Utils;

namespace GT.Net
{
    public class UdpClientTransport : BaseUdpTransport
    {
        protected UdpClient udpClient;

        public UdpClientTransport(UdpClient udpc) {
            udpClient = udpc;
        }

        public override bool Active
        {
            get { return udpClient != null; }
        }

        public void Dispose() {
            if (!Active) { return; }
            //kill the connexion as best we can
            lock (this)
            {
                try
                {
                    // udpClient.Client.LingerState.Enabled = false; // FIXME: verify not supported on UDP
                    if (udpClient != null) { udpClient.Close(); }
                }
                catch (SocketException e)
                {
                    // FIXME: logError(INFORM, "exception thrown when terminating socket", e);
                    Console.WriteLine(this + ": EXCEPTION thrown when terminating up socket: " + e);
                }
                udpClient = null;
            }
        }


        /// <summary> Flushes out old incomingMessages that couldn't be sent because of exceptions</summary>
        /// <returns>True if there are bytes that still have to be sent out</returns>
        protected override void FlushOutstandingPackets()
        {
            byte[] b;
            SocketError error = SocketError.Success;

            try
            {
                while (outstanding.Count > 0 && udpClient.Client.Connected)
                {
                    b = outstanding.Peek();
                    udpClient.Client.Send(b, 0, b.Length, SocketFlags.None, out error);

                    switch (error)
                    {
                        case SocketError.Success:
                            outstanding.Dequeue();
                            break;
                        case SocketError.WouldBlock:
                            //don't die, but try again next time
                            // FIXME: This should not be an error!
                            // FIXME: Does UDP ever cause a WouldBlock?
                            //NotifyError(null, error, "The UDP write buffer is full now, but the data will be saved and " +
                            //        "sent soon.  Send less data to reduce perceived latency.");
                            return;
                        default:
                            //something terrible happened, but this is only UDP, so stick around.
                            NotifyError("Error sending UDP packet", error);
                            return;
                    }
                }
            }
            catch (SocketException e)
            {
                NotifyError("Error sending UDP data", e);
            }
        }

        protected override void CheckIncomingPackets()
        {
            try
            {
                //while there are more packets to read
                while (udpClient.Client.Available > 0)
                {
                    IPEndPoint ep = null;
                    byte[] buffer = udpClient.Receive(ref ep);

                    DebugUtils.DumpMessage(this + ": Update()", buffer);
                    Debug.Assert(ep.Equals(udpClient.Client.RemoteEndPoint));
                    NotifyPacketReceived(buffer, 0, buffer.Length);
                }
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode != SocketError.WouldBlock)
                {
                    NotifyError("Error reading UDP data", e);
                }
            }
        }

        public override int MaximumPacketSize
        {
            get
            {
                try
                {
                    return Math.Min(CappedMessageSize, 
                        udpClient.Client.SendBufferSize);
                }
                catch (Exception)
                {
                    return CappedMessageSize;
                }
            }
        }

    }
}
