//
// GT: The Groupware Toolkit for C#
// Copyright (C) 2006 - 2009 by the University of Saskatchewan
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later
// version.
// 
// This library is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA
// 02110-1301  USA
// 

using System;
using System.Collections.Generic;
using System.Text;

namespace GT.Net
{
    /// <summary>
    /// Injects a certain amount of latency into the sending or receiving from this connexion.
    /// </summary>
    public class DelayedBinaryStream
    {
        /// <summary>
        /// The milliseconds of delay injected into messages sent on the stream
        /// </summary>
        public long InjectedDelay
        {
            get { return injectedDelayMS; }
            set { injectedDelayMS = value;  }
        }

        /// <summary>
        /// The incomingMessages which have been received by this connexion, after the 
        /// injected delay has passed
        /// </summary>
        public IList<byte[]> Messages;

        private long injectedDelayMS;
        private IBinaryStream bs = null;
        private SortedDictionary<long, byte[]> sendQueue;
        private SortedDictionary<long, byte[]> dequeueQueue;
        private HPTimer timer;

        /// <summary>Delays the sending and receiving of incomingMessages by 'injectedDelay' milliseconds.
        /// </summary>
        /// <param name="bs">The binary connexion to use to send and receive on.</param>
        /// <param name="injectedDelay">time in milliseconds to inject</param>
        public DelayedBinaryStream(IBinaryStream bs, int injectedDelay)
        {
            sendQueue = new SortedDictionary<long, byte[]>();
            dequeueQueue = new SortedDictionary<long, byte[]>();
            this.bs = bs;
            bs.BinaryNewMessageEvent += new BinaryNewMessage(BinaryNewMessageEvent);
            Messages = new List<byte[]>();
            timer = new HPTimer();
            injectedDelayMS = injectedDelay;
        }

        /// <summary>Sends a message after waiting a certain amount of time
        /// </summary>
        /// <param name="b">bytes to send as a message</param>
        public void Send(byte[] b)
        {
            timer.Update();
            long currentTime = timer.ElapsedInMilliseconds;
            long currentDelay = (long)bs.Delay;

            lock (sendQueue)
            {
                while (sendQueue.ContainsKey(currentTime))
                {
                    currentTime++;
                }
                sendQueue.Add(currentTime, b);

                SortedDictionary<long, byte[]>.Enumerator e = sendQueue.GetEnumerator();
                while (e.MoveNext())
                {
                    if (e.Current.Key + injectedDelayMS - currentDelay >= currentTime)
                    {
                        return;
                    }

                    bs.Send(e.Current.Value);
                    sendQueue.Remove(e.Current.Key);
                    e.Dispose();
                    e = sendQueue.GetEnumerator();
                }
            }
        }

        /// <summary>Checks to see if anything queued should be sent.
        /// The sending resolution of this connexion is as good as 
        /// the frequency this method and the Send method are called.
        /// </summary>
        public void SendCheck()
        {
            timer.Update();
            long currentTime = timer.TimeInMilliseconds;
            long currentDelay = (long)bs.Delay;

            lock (sendQueue)
            {
                SortedDictionary<long, byte[]>.Enumerator e = sendQueue.GetEnumerator();
                while (e.MoveNext())
                {
                    if (e.Current.Key + injectedDelayMS - currentDelay >= currentTime)
                    {
                        return;
                    }

                    bs.Send(e.Current.Value);

                    sendQueue.Remove(e.Current.Key);
                    e.Dispose();
                    e = sendQueue.GetEnumerator();
                }
            }
        }

        /// <summary>Dequeues the oldest message that is ready to be received
        /// </summary>
        /// <param name="index">the index of the message to dequeue</param>
        /// <returns>the oldest message</returns>
        public byte[] DequeueMessage(int index)
        {
            timer.Update();
            long currentTime = timer.ElapsedInMilliseconds;
            long currentDelay = (long)bs.Delay;
            byte[] b;

            lock (dequeueQueue)
            {
                SortedDictionary<long, byte[]>.Enumerator e = dequeueQueue.GetEnumerator();

                //if empty, return
                while (e.MoveNext())
                {
                    if (e.Current.Key + injectedDelayMS - currentDelay >= currentTime)
                    {
                        break;
                    }
                    dequeueQueue.Remove(e.Current.Key);
                    Messages.Add(e.Current.Value);
                    e.Dispose();
                    e = dequeueQueue.GetEnumerator();
                }

                //return their message
                if (Messages.Count <= index) { return null; }
                b = Messages[index];
                Messages.RemoveAt(index);
                return b;
            }
        }

        void BinaryNewMessageEvent(IBinaryStream stream)
        {
            timer.Update();
            long currentTime = timer.ElapsedInMilliseconds;

            lock (dequeueQueue)
            {
                if (bs.Messages.Count > 0)
                {
                    byte[] b;
                    while ((b = bs.DequeueMessage(0)) != null)
                    {
                        while (dequeueQueue.ContainsKey(currentTime))
                            currentTime += 1;
                        dequeueQueue.Add(currentTime, b);
                    }
                }
            }
        }

    }
}
