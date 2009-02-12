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
        public TimeSpan InjectedDelay { get; set; }

        /// <summary>
        /// The incomingMessages which have been received by this connexion, after the 
        /// injected delay has passed
        /// </summary>
        public IList<byte[]> Messages;

        private IBinaryStream bs = null;
        private SortedDictionary<long, byte[]> sendQueue;
        private SortedDictionary<long, byte[]> dequeueQueue;
        private HPTimer timer;

        /// <summary>Delays the sending and receiving of incomingMessages by 'injectedDelay' milliseconds.
        /// </summary>
        /// <param name="bs">The binary connexion to use to send and receive on.</param>
        /// <param name="injectedDelay">delay time to inject</param>
        public DelayedBinaryStream(IBinaryStream bs, TimeSpan injectedDelay)
        {
            sendQueue = new SortedDictionary<long, byte[]>();
            dequeueQueue = new SortedDictionary<long, byte[]>();
            this.bs = bs;
            bs.MessagesReceived += BinaryNewMessageEvent;
            Messages = new List<byte[]>();
            timer = new HPTimer();
            InjectedDelay = injectedDelay;
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
                    if (e.Current.Key + InjectedDelay.TotalMilliseconds - currentDelay >= currentTime)
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
                    if (e.Current.Key + InjectedDelay.TotalMilliseconds - currentDelay >= currentTime)
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
                    if (e.Current.Key + InjectedDelay.TotalMilliseconds - currentDelay >= currentTime)
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
