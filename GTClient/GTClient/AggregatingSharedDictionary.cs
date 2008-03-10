using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using GT.Net;

namespace GT.Net
{
    class AggregatingSharedDictionary
    {
        /// <summary>
        /// This method will handle a change event
        /// </summary>
        /// <param name="key">The key that has been changed</param>
        public delegate void Change(string key);

        /// <summary>
        /// Triggered when a key has been updated
        /// </summary>
        public event Change ChangeEvent;

        /// <summary>
        /// Keys on this list will not be updated from the network.  This is useful for security
        /// purposes, to prevent someone else from changing your own telepointer or avatar position or
        /// whatever else without your permission.
        /// </summary>
        public List<string> Master;

        /// <summary>
        /// This is a very simple implementation of a shared dictionary.  It can be used
        /// like an array of serializable objects of unknown size, where strings are used 
        /// as keys.  If an object is put inside the array, it is pushed to other copies
        /// of the dictionary connected to the same network and channel.  If an object is
        /// taken from the array, the latest cached copy is returned.  If there is no
        /// cached copy yet (that is, if the client is relatively new), then null is returned,
        /// but the object is requested from other shared dictionaries on the network and 
        /// channel.  The emergent behaviour is a very simple shared dictionary that performs
        /// extremely fast reads but expensive writes, and is ideal for non-streaming datasets.
        /// Examples of information that would be great to store in this dictionary would be
        /// user information like colour, preferences, avatar appearance, and object descriptions.
        /// Consistancy is achieved by assuming that each client only writes to their own keys,
        /// like "myclientname/objectname/whatever", or that writes are infrequeny enough that
        /// there is very little chance that two client will try to write to an object at the same
        /// time.  When an object is changed, an event is triggered describing which object has
        /// been updated.
        /// </summary>
        /// <param name="bs">A networked binary connection of some sort.  A compressed connection is recommended.</param>
        /// <param name="milliseconds">Never send immediately unless this amount of time has passed.</param>
        public AggregatingSharedDictionary(IBinaryStream bs, int milliseconds)
        {
            this.stream = bs;
            this.lastTimeSent = 0;
            this.currentTime = 1;
            this.interval = 2;
            this.milliseconds = milliseconds;
            this.Master = new List<string>();
            this.sharedList = new Dictionary<string, object>();
            this.stream.BinaryNewMessageEvent += new BinaryNewMessage(bs_BinaryNewMessageEvent);
            this.stream.UpdateEvent += new UpdateEventDelegate(stream_UpdateEvent);
        }

        /// <summary>
        /// Treat this shared dictionary as an array.  Remember that changes are only broadcast when
        /// an object is put back into it.
        /// </summary>
        /// <param name="key">A string of any length representing a dictionary key</param>
        /// <returns>An object tied to this key</returns>
        public object this[string key]
        {
            get
            {
                if(sharedList.ContainsKey(key))
                    return sharedList[key];
                PullKey(key);
                return null;
            }

            set
            {
                //update our copy
                sharedList[key] = value;
                PushKey(key, value);
            }
        }

        /// <summary>
        /// A unique number that no other client has in relation to this server.
        /// If zero, don't use.  It will be assigned a unique number by the server soon after connecting.
        /// </summary>
        public int UniqueIdentity
        {
            get
            {
                return stream.UniqueIdentity;
            }
        }

        #region Private

        private static BinaryFormatter formatter = new BinaryFormatter();

        IBinaryStream stream;
        Dictionary<string, object> sharedList;
        private long currentTime;
        private long lastTimeSent;  //last time we sent
        private long interval;  //wait this long between sendings
        private bool oneMessageOrMoreWaiting;
        private int milliseconds; //wait this long between sendings

        //flush channel if there are possible updates to send.
        void stream_UpdateEvent(HPTimer hpTimer)
        {
            interval = hpTimer.Frequency * milliseconds / 1000;
            currentTime = hpTimer.Time;

            //check to see if there are possibly messages to send
            if(!oneMessageOrMoreWaiting)
                return;
            
            if (lastTimeSent + interval > hpTimer.Time)
                return;

            this.stream.FlushAllOutgoingMessagesOnChannel(MessageProtocol.Tcp);
        }

        private void bs_BinaryNewMessageEvent(IBinaryStream stream)
        {
            byte[] b;
            while ((b = stream.DequeueMessage(0)) != null)
            {
                short nameLength = BitConverter.ToInt16(b, 0);
                string name = ASCIIEncoding.ASCII.GetString(b, 2, nameLength);

                //it's a push!
                if (2 + nameLength < b.Length)
                {
                    //we don't accept updates on personal objects;
                    if(Master.Contains(name))
                        return;

                    int objectLength = b.Length - (2 + name.Length);
                    object obj = BytesToObject(b, (2 + name.Length), objectLength);

                    //update or add our copy
                    if (sharedList.ContainsKey(name))
                        sharedList[name] = obj;
                    else
                        sharedList.Add(name, obj);

                    //inform our app that it has been updated
                    if (ChangeEvent != null )
                        ChangeEvent(name);
                }

                //it's a pull.  if we have a copy, send it
                else
                {
                    if(sharedList.ContainsKey(name))
                        PushKey(name, sharedList[name]);
                    return;
                }
            }
        }

        private void PullKey(string key)
        {
            //pull key
            byte[] nameInBytes = ASCIIEncoding.ASCII.GetBytes(key);
            byte[] lengthInBytes = BitConverter.GetBytes((short)nameInBytes.Length);
            byte[] buffer = new byte[2 + nameInBytes.Length];
            Array.Copy(lengthInBytes, 0, buffer, 0, 2);
            Array.Copy(nameInBytes, 0, buffer, 2, nameInBytes.Length);
            stream.Send(buffer);
        }

        //only send if we haven't done so in a while
        private void PushKey(string key, object obj)
        {
            //push key and object
            byte[] nameInBytes = ASCIIEncoding.ASCII.GetBytes(key);
            byte[] objectInBytes = ObjectsToBytes(obj);
            byte[] lengthInBytes = BitConverter.GetBytes((short)nameInBytes.Length);
            byte[] buffer = new byte[2 + nameInBytes.Length + objectInBytes.Length];
            Array.Copy(lengthInBytes, 0, buffer, 0, 2);
            Array.Copy(nameInBytes, 0, buffer, 2, nameInBytes.Length);
            Array.Copy(objectInBytes, 0, buffer, 2 + nameInBytes.Length, objectInBytes.Length);

            //if we've already sent stuff in this interval, aggregate instead of sending
            if (lastTimeSent + interval < currentTime)
            {
                stream.Send(buffer, MessageProtocol.Tcp, MessageAggregation.Yes, MessageOrder.AllChannel);
                this.oneMessageOrMoreWaiting = true;
            }
            else
            {
                stream.Send(buffer, MessageProtocol.Tcp, MessageAggregation.No, MessageOrder.AllChannel);
                this.oneMessageOrMoreWaiting = false;
            }

            return;
        }

        private byte[] ObjectsToBytes(object o)
        {
            MemoryStream ms = new MemoryStream();
            formatter.Serialize(ms, o);
            byte[] buffer = new byte[ms.Position];
            ms.Position = 0;
            ms.Read(buffer, 0, buffer.Length);
            return buffer;
        }

        private object BytesToObject(byte[] b)
        {
            return BytesToObject(b, 0, b.Length);
        }

        private object BytesToObject(byte[] b, int startIndex, int length)
        {
            MemoryStream ms = new MemoryStream();
            ms.Write(b, startIndex, length);
            ms.Position = 0;
            return formatter.Deserialize(ms);
        }

        #endregion
    }
}
