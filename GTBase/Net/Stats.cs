using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace GT.Net
{
    ///<summary>
    /// A class gathering statistics on the networking behaviour of a 
    /// communicator (a GT Client or a GT Server).
    ///</summary>
    public class CommunicationStatisticsObserver<C> : IDisposable
        where C: Communicator
    {
        protected StatisticsSnapshot snapshot;
 
        public C Observed { get; protected set; }

        public static CommunicationStatisticsObserver<C> On<C>(C communicator)
            where C: Communicator
        {
            CommunicationStatisticsObserver<C> sd = new CommunicationStatisticsObserver<C>();
            sd.Observe(communicator);
            return sd;
        }

        protected CommunicationStatisticsObserver()
        {
            Paused = false;
            Reset();
        }

        public bool Paused { get; set; }

        public void Observe(C communicator)
        {
            Observed = communicator;
            foreach (IConnexion c in communicator.Connexions)
            {
                _connexionAdded(communicator, c);
            }
            communicator.ConnexionAdded += _connexionAdded;
            communicator.ConnexionRemoved += _connexionRemoved;
            snapshot.ConnexionCount = communicator.Connexions.Count;
        }

        public void Dispose()
        {
            Observed.ConnexionAdded += _connexionAdded;
            Observed.ConnexionRemoved += _connexionRemoved;
            foreach (IConnexion c in Observed.Connexions)
            {
                _connexionRemoved(Observed, c);
            }
        }

        /// <summary>
        /// Reset the values that are updated each tick.
        /// </summary>
        public StatisticsSnapshot Reset()
        {
            StatisticsSnapshot old = Interlocked.Exchange(ref snapshot, new StatisticsSnapshot());
            if (old == null) { return null; }
            old.ScrobbleConnexions(Observed.Connexions);
            return old;
        }

        /// <summary>
        /// Take a snapshot of the accumulated values.
        /// </summary>
        public StatisticsSnapshot Snapshot()
        {
            if (snapshot == null) { return null; }
            StatisticsSnapshot clone = snapshot.Clone();
            clone.ConnexionCount = snapshot.ConnexionCount = Observed.Connexions.Count;
            return clone;
        }

        #region Events

        private void _connexionAdded(Communicator ignored, IConnexion c)
        {
            lock (this)
            {
                snapshot.ConnexionCount = Observed.Connexions.Count;

                // dup'd in server_ClientsJoined
                c.MessageReceived += connexion_MessageReceived;
                c.MessageSent += connexion_MessageSent;
                c.TransportAdded += connexion_TransportAdded;
                foreach (ITransport t in c.Transports) { connexion_TransportAdded(c, t); }
            }
        }

        private void _connexionRemoved(Communicator ignored, IConnexion c)
        {
            lock (this)
            {
                snapshot.ConnexionCount = Observed.Connexions.Count;

                c.MessageReceived -= connexion_MessageReceived;
                c.MessageSent -= connexion_MessageSent;
                c.TransportAdded -= connexion_TransportAdded;
            }
        }

        private void connexion_TransportAdded(IConnexion connexion, ITransport newTransport)
        {
            newTransport.PacketSentEvent += transport_PacketSent;
            newTransport.PacketReceivedEvent += transport_PacketReceived;
        }

        private void transport_PacketReceived(byte[] buffer, int offset, int count, ITransport transport)
        {
            snapshot.NotifyPacketReceived(count, transport);
        }

        private void transport_PacketSent(byte[] buffer, int offset, int count, ITransport transport)
        {
            snapshot.NotifyPacketSent(count, transport);
        }

        private void connexion_MessageReceived(Message m, IConnexion source, ITransport transport)
        {
            if (m.MessageType == MessageType.System) { return; }
            snapshot.NotifyMessageReceived(m, source, transport);
        }

        private void connexion_MessageSent(Message m, IConnexion dest, ITransport transport)
        {
            if (m.MessageType == MessageType.System) { return; }
            snapshot.NotifyMessageSent(m, dest, transport);
        }

        #endregion
    }

    public class StatisticsSnapshot
    {
        protected IList<string> transportNames;

        protected IDictionary<byte, IDictionary<MessageType, IDictionary<string, int>>> messagesReceivedCounts =
            new Dictionary<byte, IDictionary<MessageType, IDictionary<string, int>>>();
        protected IDictionary<byte, IDictionary<MessageType, IDictionary<string, int>>> messagesSentCounts =
            new Dictionary<byte, IDictionary<MessageType, IDictionary<string, int>>>();

        protected IDictionary<string, int> msgsRecvPerTransport = new Dictionary<string, int>();
        protected IDictionary<string, int> msgsSentPerTransport = new Dictionary<string, int>();

        protected IDictionary<string, int> bytesRecvPerTransport = new Dictionary<string, int>();
        protected IDictionary<string, int> bytesSentPerTransport = new Dictionary<string, int>();
        protected IDictionary<IConnexion, IDictionary<String, float>> delays =
            new Dictionary<IConnexion, IDictionary<String, float>>();
        protected IDictionary<IConnexion, IDictionary<String, uint>> backlogs = 
            new Dictionary<IConnexion, IDictionary<String, uint>>();

        public StatisticsSnapshot()
        {
            BytesReceived = 0;
            BytesSent = 0;
            MessagesReceived = 0;
            MessagesSent = 0;
            ConnexionCount = 0;
        }

        public IList<string> TransportNames
        {
            get
            {
                if (transportNames == null)
                {
                    IDictionary<string, string> tns = new Dictionary<string, string>();
                    foreach (string key in msgsRecvPerTransport.Keys)
                    {
                        tns[key] = key;
                    }
                    foreach (string key in msgsSentPerTransport.Keys)
                    {
                        tns[key] = key;
                    }
                    foreach (string key in bytesRecvPerTransport.Keys)
                    {
                        tns[key] = key;
                    }
                    foreach (string key in bytesSentPerTransport.Keys)
                    {
                        tns[key] = key;
                    }
                    List<string> result = new List<string>(tns.Keys);
                    result.Sort();
                    transportNames = result;
                }
                return transportNames;
            }
        }


        /// <summary>
        /// How many connexions does the communicator have open?
        /// </summary>
        public int ConnexionCount { get; protected internal set; }

        public int MessagesSent { get; protected set; }
        public int MessagesReceived { get; protected set; }

        public int BytesSent { get; protected set; }
        public int BytesReceived { get; protected set; }

        public IDictionary<string, int> MessagesSentPerTransport
        {
            get { return msgsSentPerTransport; }
        }
        public IDictionary<string, int> MessagesReceivedPerTransport
        {
            get { return msgsRecvPerTransport; }
        }

        public IDictionary<string, int> BytesSentPerTransport
        {
            get { return bytesSentPerTransport; }
        }
        public IDictionary<string, int> BytesReceivedPerTransport
        {
            get { return bytesRecvPerTransport; }
        }

        public IEnumerable<byte> SentChannels
        {
            get { return messagesSentCounts.Keys; }
        }

        public IEnumerable<byte> ReceivedChannels
        {
            get { return messagesSentCounts.Keys; }
        }

        public IDictionary<IConnexion, IDictionary<string, uint>> Backlogs
        {
            get { return backlogs; }
        }

        public IDictionary<IConnexion, IDictionary<string, float>> Delays
        {
            get { return delays; }
        }

        internal void NotifyPacketReceived(int count, ITransport transport)
        {
            transportNames = null;

            BytesReceived += count;

            // Record the bytes per transport
            int value;
            if (!bytesRecvPerTransport.TryGetValue(transport.Name, out value)) { value = 0; }
            bytesRecvPerTransport[transport.Name] = value + count;
        }

        internal void NotifyPacketSent(int count, ITransport transport)
        {
            transportNames = null;

            BytesSent += count;
            int value;

            // Record the bytes per transport
            if (!bytesSentPerTransport.TryGetValue(transport.Name, out value)) { value = 0; }
            bytesSentPerTransport[transport.Name] = value + count;
        }

        internal void NotifyMessageReceived(Message m, IConnexion source, ITransport transport)
        {
            transportNames = null;

            MessagesReceived++;
            int value;

            // Record the messages per transport
            if (!msgsRecvPerTransport.TryGetValue(transport.Name, out value)) { value = 0; }
            msgsRecvPerTransport[transport.Name] = value + 1;

            // Record the messages per channel per message-type
            IDictionary<MessageType, IDictionary<string, int>> subdict;
            if (!messagesReceivedCounts.TryGetValue(m.Id, out subdict))
            {
                subdict = messagesReceivedCounts[m.Id] =
                    new Dictionary<MessageType, IDictionary<string, int>>();
            }
            IDictionary<string, int> transDict;
            if (!subdict.TryGetValue(m.MessageType, out transDict))
            {
                transDict = subdict[m.MessageType] = new Dictionary<string, int>();
            }
            if (!transDict.TryGetValue(transport.Name, out value))
            {
                value = 0;
            }
            transDict[transport.Name] = ++value;
        }

        internal void NotifyMessageSent(Message m, IConnexion dest, ITransport transport)
        {
            transportNames = null;

            MessagesSent++;
            int value;

            // Record the messages per transport
            if (!msgsSentPerTransport.TryGetValue(transport.Name, out value)) { value = 0; }
            msgsSentPerTransport[transport.Name] = value + 1;

            // Record the messages per channel per message-type
            IDictionary<MessageType, IDictionary<string, int>> subdict;
            if (!messagesSentCounts.TryGetValue(m.Id, out subdict))
            {
                subdict = messagesSentCounts[m.Id] =
                    new Dictionary<MessageType, IDictionary<string, int>>();
            }
            IDictionary<string, int> transDict;
            if (!subdict.TryGetValue(m.MessageType, out transDict))
            {
                transDict = subdict[m.MessageType] = new Dictionary<string, int>();
            }
            if (!transDict.TryGetValue(transport.Name, out value))
            {
                value = 0;
            }
            transDict[transport.Name] = ++value;
        }

        public StatisticsSnapshot Clone()
        {
            StatisticsSnapshot copy = new StatisticsSnapshot();
            copy.ConnexionCount = ConnexionCount;
            copy.BytesReceived = BytesReceived;
            copy.BytesSent = BytesSent;
            copy.bytesSentPerTransport = new Dictionary<string, int>(bytesSentPerTransport);
            copy.bytesRecvPerTransport = new Dictionary<string, int>(bytesRecvPerTransport);
            copy.ConnexionCount = ConnexionCount;
            copy.MessagesReceived = MessagesReceived;
            copy.MessagesSent = MessagesSent;
            copy.messagesReceivedCounts = DeepCopy(messagesReceivedCounts);
            copy.messagesSentCounts = DeepCopy(messagesSentCounts);
            return copy;
        }

        private T DeepCopy<T>(T original)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream ms = new MemoryStream(64); // 64 is a guestimate
            formatter.Serialize(ms, original);
            ms.Position = 0;
            return (T)formatter.Deserialize(ms);
        }

        public int ComputeMessagesSentByTransport(string tn)
        {
            //(int)messagesSent.Compute("Sum(" + _stats.IndexMessageCount + ")", _stats.IndexTransportName + " = " + tn);
            int count = 0;
            foreach (IDictionary<MessageType, IDictionary<string, int>> subdict in messagesSentCounts.Values)
            {
                foreach (IDictionary<string, int> tdict in subdict.Values)
                {
                    int value;
                    if (tdict.TryGetValue(tn, out value)) { count += value; }
                }
            }
            return count;
        }

        public int ComputeMessagesReceivedByTransport(string tn)
        {
            //(int)messagesReceived.Compute("Sum(" + _stats.IndexMessageCount + ")", _stats.IndexTransportName + " = " + tn);
            int count = 0;
            foreach (IDictionary<MessageType, IDictionary<string, int>> subdict in messagesReceivedCounts.Values)
            {
                foreach (IDictionary<string, int> tdict in subdict.Values)
                {
                    int value;
                    if (tdict.TryGetValue(tn, out value)) { count += value; }
                }
            }
            return count;
        }

        public int ComputeBytesSentByTransport(string tn)
        {
            //(int)sent.Compute("Sum(" + _stats.IndexByteCount + ")", _stats.IndexTransportName + " = " + tn);
            int value;
            return bytesSentPerTransport.TryGetValue(tn, out value) ? value : 0;
        }

        public int ComputeBytesReceivedByTransport(string tn)
        {
            //(int)sent.Compute("Sum(" + _stats.IndexByteCount + ")", _stats.IndexTransportName + " = " + tn);
            int value;
            return BytesReceivedPerTransport.TryGetValue(tn, out value) ? value : 0;
        }

        public int ComputeMessagesSent(byte channel, MessageType mt)
        {
            //(int)sent.Compute("Sum(" + _stats.IndexMessageCount + ")", _stats.IndexChannel + " = " + channel + " AND " + _stats.IndexMessageType + " = " + t.ToString());
            int count = 0;
            IDictionary<MessageType, IDictionary<string, int>> subdict;
            if(messagesSentCounts.TryGetValue(channel, out subdict) && subdict.ContainsKey(mt)) {
                foreach (int value in subdict[mt].Values)
                {
                    count += value;
                }
            }
            return count;
        }

        public int ComputeMessagesReceived(byte channel, MessageType mt)
        {
            //(int)received.Compute("Sum(" + _stats.IndexMessageCount + ")", _stats.IndexChannel + " = " + channel + " AND " + _stats.IndexMessageType + " = " + t.ToString());
            int count = 0;
            IDictionary<MessageType, IDictionary<string, int>> subdict;
            if (messagesReceivedCounts.TryGetValue(channel, out subdict) && subdict.ContainsKey(mt))
            {
                foreach (int value in subdict[mt].Values)
                {
                    count += value;
                }
            }
            return count;
        }

        internal void ScrobbleConnexions(ICollection<IConnexion> connexions)
        {
            ConnexionCount = connexions.Count;
            foreach(IConnexion cnx in connexions)
            {
                uint backlog = 0;
                foreach(ITransport t in cnx.Transports)
                {
                    if(!delays.ContainsKey(cnx)) { delays[cnx] = new Dictionary<string, float>(); }
                    delays[cnx][t.Name] = t.Delay;

                    if(!backlogs.ContainsKey(cnx)) { backlogs[cnx] = new Dictionary<string, uint>(); }
                    backlogs[cnx][t.Name] = backlog;
                }
            }
        }
    }
}
