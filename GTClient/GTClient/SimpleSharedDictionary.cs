using System;
using System.Collections.Generic;

namespace GT.Net
{
    /// <summary>
    /// This is a very simple implementation of a shared dictionary.  It can be used
    /// like an array of serializable objects of unknown size, where strings are used 
    /// as keys.  If an object is put inside the array, it is pushed to other copies
    /// of the dictionary connected to the same network and channel.  If an object is
    /// taken from the array, the latest cached copy is returned.  If there is no
    /// cached copy yet (that is, if the client is relatively new), then null is returned,
    /// but the object is requested from other shared dictionaries on the network and 
    /// channelId.  The emergent behaviour is a very simple shared dictionary that performs
    /// extremely fast reads but expensive writes, and is ideal for non-streaming datasets.
    /// Examples of information that would be great to store in this dictionary would be
    /// user information like colour, preferences, avatar appearance, and object descriptions.
    /// Consistancy is achieved by assuming that each client only writes to their own keys,
    /// like "myclientname/objectname/whatever", or that writes are infrequeny enough that
    /// there is very little chance that two client will try to write to an object at the same
    /// time.  When an object is changed, an event is triggered describing which object has
    /// been updated.
    /// </summary>
    public class SimpleSharedDictionary
    {
        /// <summary>
        /// This method will handle a change event
        /// </summary>
        /// <param name="key">The key that has been changed</param>
        public delegate void Change(string key);

        /// <summary>
        /// Triggered when a key has been updated
        /// </summary>
        public event Change Changed;

        /// <summary>
        /// Keys on this list will not be updated from the network.  This is useful for security
        /// purposes, to prevent someone else from changing your own telepointer or avatar position or
        /// whatever else without your permission.
        /// </summary>
        public IList<string> Master;

        /// <summary>Create a new shared dictionary.</summary>
        /// <param name="s">A networked object channel.</param>
        public SimpleSharedDictionary(IObjectChannel s)
        {
            this.channel = s;
            this.channel.MessagesReceived += channel_NewMessageEvent;
            this.Master = new List<string>();
            this.sharedDictionary = new Dictionary<string, object>();
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
                if (sharedDictionary.ContainsKey(key))
                {
                    return sharedDictionary[key];
                }
                PullKey(key);
                return null;
            }

            set
            {
                //update our copy
                sharedDictionary[key] = value;
                PushKey(key, value);
            }
        }

        /// <summary>
        /// Does this instance contain a value for <c>key</c>?  A false result does not mean that
        /// the group dictionary does not actually contain this value.  This method
        /// is mostly for testing purposes.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool ContainsKey(string key)
        {
            return sharedDictionary.ContainsKey(key);
        }

        /// <summary>
        /// A unique number that no other client has in relation to this server.
        /// If zero, don't use.  It will be assigned a unique number by the server soon after connecting.
        /// </summary>
        /// <seealso cref="IChannel.Identity"/>
        public int Identity
        {
            get
            {
                return channel.Identity;
            }
        }

        #region Implementation

        protected IObjectChannel channel;
        protected Dictionary<string, object> sharedDictionary;

        private void channel_NewMessageEvent(IObjectChannel channel)
        {
            object o;
            while ((o = channel.DequeueMessage(0)) != null)
            {
                if (o is KeyValuePair<string, object>)    // push
                {
                    KeyValuePair<string, object> kvp = (KeyValuePair<string, object>)o;
                    //we don't accept updates on personal objects;
                    if (Master.Contains(kvp.Key))
                    {
                        //Console.WriteLine("{0}: key on master list: ignored push: {1}", this, kvp);
                        return;
                    }

                    //Console.WriteLine("{0}: received push: {1}", this, kvp);
                    //update or add our copy
                    if (sharedDictionary.ContainsKey(kvp.Key))
                    {
                        sharedDictionary[kvp.Key] = kvp.Value;
                    }
                    else
                    {
                        sharedDictionary.Add(kvp.Key, kvp.Value);
                    }

                    //inform our app that it has been updated
                    if (Changed != null) { Changed(kvp.Key); }
                }
                else if (o is string)
                {
                    //it's a pull.  if we have a copy, send it
                    string name = (string)o;
                    if (sharedDictionary.ContainsKey(name))
                    {
                        //Console.WriteLine("{0}: fulfilling request for: {1}", this, name);
                        PushKey(name, sharedDictionary[name]);
                    }
                    //else
                    //{
                    //    Console.WriteLine("{0}: cannot satisfy request: {1}", this, name);
                    //}
                }
                else
                {
                    Console.WriteLine("{0}: WARNING: received invalid object: {1} (ignored)", this, o);
                }
            }
        }

        virtual protected void PullKey(string key)
        {
            channel.Send(key);   // strings indicate a request
        }

        virtual protected void PushKey(string key, object obj)
        {
            channel.Send(new KeyValuePair<string, object>(key, obj));
        }

        #endregion

        virtual public void Flush()
        {
            channel.Flush();
        }

        override public string ToString()
        {
            return GetType().Name + "(" + channel + ")";
        }
    }

    /// <summary>
    /// FIXME: Question: is this actually necessary?  Can't the channel be
    /// aggregating?
    /// </summary>
    public class AggregatingSharedDictionary : SimpleSharedDictionary
    {

        /// <summary>Create a new shared dictionary.</summary>
        /// <param name="s">A networked object channel.</param>
        /// <param name="updateTime">Batch updates until this amount of time has passed.</param>
        public AggregatingSharedDictionary(IObjectChannel s, TimeSpan updateTime)
            : base(s)
        {
            channel.Updated += channel_UpdateEvent;
            lastTimeSent = 0;
            this.updateTime = updateTime;
        }

        private bool sendsPending = false;
        private long lastTimeSent;  //last time we sent (in milliseconds)
        private TimeSpan updateTime; //wait this long between sendings

        //flush channel if there are possible updates to send.
        void channel_UpdateEvent(HPTimer hpTimer)
        {
            //check to see if there are possibly incomingMessages to send
            if (!sendsPending) { return; }
            // One might ask: shouldn't this be better done through the channel aggregation?
            if (lastTimeSent + updateTime.TotalMilliseconds > hpTimer.TimeInMilliseconds) { return; }
            Flush();
            lastTimeSent = hpTimer.TimeInMilliseconds;
        }

        override protected void PullKey(string key)
        {
            base.PullKey(key);
            sendsPending = true;
        }

        override protected void PushKey(string key, object obj)
        {
            base.PushKey(key, obj);
            sendsPending = true;
        }

        public override void Flush()
        {
            //if(!sendsPending) { return; }
            base.Flush();
            sendsPending = false;
        }
    }
}
