using System;
using System.Collections.Generic;
using System.Text;

namespace GTClient
{
    /// <summary>
    /// Injects a certain amount of latency into the sending or receiving from this connection.
    /// </summary>
    public class DelayedBinaryStream
    {
        /// <summary>
        /// The milliseconds of delay injected into the connection
        /// </summary>
        public int InjectedDelay
        {
            get { return (int)(injectedDelayInTicks / timer.Frequency * 1000); }
            set { injectedDelayInTicks = value * timer.Frequency / 1000;  }
        }

        /// <summary>
        /// The messages which have been received by this connection, after the injected delay has passed
        /// </summary>
        public List<byte[]> Messages;

        private long injectedDelayInTicks;
        private IBinaryStream bs = null;
        private SortedDictionary<long, byte[]> sendQueue;
        private SortedDictionary<long, byte[]> dequeueQueue;
        private HPTimer timer;

        /// <summary>Delays the sending and receiving of messages by 'injectedDelay' milliseconds.
        /// </summary>
        /// <param name="bs">The binary connection to use to send and receive on.</param>
        /// <param name="injectedDelay">time in milliseconds to inject</param>
        public DelayedBinaryStream(IBinaryStream bs, int injectedDelay)
        {
            sendQueue = new SortedDictionary<long, byte[]>();
            dequeueQueue = new SortedDictionary<long, byte[]>();
            this.bs = bs;
            bs.BinaryNewMessageEvent += new BinaryNewMessage(BinaryNewMessageEvent);
            Messages = new List<byte[]>();
            timer = new HPTimer();
            this.InjectedDelay = injectedDelay;
        }

        /// <summary>Sends a message after waiting a certain amount of time
        /// </summary>
        /// <param name="b">bytes to send as a message</param>
        public void Send(byte[] b)
        {
            timer.Update();
            long currentTime = timer.Time;
            long currentDelay = (long)(bs.Delay * timer.Frequency / 1000);

            lock (sendQueue)
            {
                while (sendQueue.ContainsKey(currentTime))
                    currentTime++;
                sendQueue.Add(currentTime, b);

                SortedDictionary<long, byte[]>.Enumerator e = sendQueue.GetEnumerator();
                while (e.MoveNext())
                {
                    if (e.Current.Key + injectedDelayInTicks - currentDelay >= currentTime)
                        return;

                    bs.Send(e.Current.Value);

                    sendQueue.Remove(e.Current.Key);
                    e = sendQueue.GetEnumerator();
                }
            }
        }

        /// <summary>Checks to see if anything queued should be sent.
        /// The sending resolution of this connection is as good as 
        /// the frequency this method and the Send method are called.
        /// </summary>
        public void SendCheck()
        {
            timer.Update();
            long currentTime = timer.Time;
            long currentDelay = (long)(bs.Delay * timer.Frequency / 1000);

            lock (sendQueue)
            {
                SortedDictionary<long, byte[]>.Enumerator e = sendQueue.GetEnumerator();
                while (e.MoveNext())
                {
                    if (e.Current.Key + injectedDelayInTicks - currentDelay >= currentTime)
                        return;

                    bs.Send(e.Current.Value);

                    sendQueue.Remove(e.Current.Key);
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
            long currentTime = timer.Time;
            long currentDelay = (long)(bs.Delay * timer.Frequency / 1000);
            byte[] b;

            lock (dequeueQueue)
            {
                SortedDictionary<long, byte[]>.Enumerator e = dequeueQueue.GetEnumerator();

                //if empty, return
                while (e.MoveNext())
                {
                    if (e.Current.Key + injectedDelayInTicks - currentDelay >= currentTime)
                        break;
                    dequeueQueue.Remove(e.Current.Key);
                    Messages.Add(e.Current.Value);
                    e = dequeueQueue.GetEnumerator();
                }

                //return their message
                if (Messages.Count <= index)
                    return null;
                b = Messages[index];
                Messages.RemoveAt(index);
                return b;
            }
        }

        void BinaryNewMessageEvent(IBinaryStream stream)
        {
            timer.Update();
            long currentTime = timer.Time;

            lock (dequeueQueue)
            {
                byte[] b;
                if (bs.Messages.Count > 0)
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
