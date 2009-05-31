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
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Collections.Generic;

namespace GT.Net
{
    public class CannotSendMessagesError : GTCompositeException
    {
        protected IDictionary<Exception, IList<PendingMessage>> messages;

        /// <summary>
        /// Return true if this exception has had errors associated with it.
        /// </summary>
        public bool IsApplicable
        {
            get { return messages != null && messages.Count > 0; }
        }

        public CannotSendMessagesError(IConnexion source)
            : base(Severity.Warning)
        {
            SourceComponent = source;
        }

        public CannotSendMessagesError(IConnexion source, Exception ex, PendingMessage msg)
            : this(source)
        {
            Add(ex, msg);
        }

        public CannotSendMessagesError(IConnexion source, Exception ex, ICollection<PendingMessage> msgs)
            : this(source)
        {
            AddAll(ex, msgs);
        }

        public CannotSendMessagesError(IConnexion source, Exception ex, Message msg)
            : this(source)
        {
            Add(ex, msg);
        }

        public CannotSendMessagesError(IConnexion source, Exception ex, ICollection<Message> msgs)
            : this(source)
        {
            AddAll(ex, msgs);
        }

        override public ICollection<Exception> SubExceptions
        {
            get
            {
                if (messages == null) { return null; }
                return messages.Keys;
            }
        }

        public IDictionary<Exception, IList<PendingMessage>> Messages { get { return messages; } }

        public void Add(Exception e, PendingMessage m)
        {
            IList<PendingMessage> list;
            if (messages == null) { messages = new Dictionary<Exception, IList<PendingMessage>>(); }
            if (!messages.TryGetValue(e, out list)) { list = messages[e] = new List<PendingMessage>(); }
            list.Add(m);
        }

        public void AddAll(Exception e, ICollection<PendingMessage> msgs)
        {
            IList<PendingMessage> list;
            if (messages == null) { messages = new Dictionary<Exception, IList<PendingMessage>>(); }
            if (!messages.TryGetValue(e, out list)) { list = messages[e] = new List<PendingMessage>(); }
            foreach (PendingMessage m in msgs) { list.Add(m); }
        }

        public void Add(Exception e, Message m)
        {
            Add(e, new PendingMessage(m, null, null));
        }

        public void AddAll(Exception e, ICollection<Message> msgs)
        {
            foreach (Message m in msgs) { Add(e, new PendingMessage(m, null, null)); }
        }

        public void ThrowIfApplicable()
        {
            if (IsApplicable)
            {
                throw this;
            }
        }
    }

    /// <summary>
    /// Represents an error situation where a connection cannot be established for
    /// some reason.
    /// </summary>
    /// <remarks>
    /// This class is serializable to support GT-Millipede,
    /// though the deserialized form may not correspond directly
    /// if some of the relevant objects are not themselves serializable.
    /// </remarks>
    [Serializable]
    public class CannotConnectException : GTException, ISerializable
    {
        public CannotConnectException(string m)
            : base(Severity.Error, m)
        { }

        public CannotConnectException(string m, Exception e)
            : base(Severity.Error, m, e)
        { }

        protected CannotConnectException(SerializationInfo info, StreamingContext context) 
            : base((Severity)info.GetInt32("severity"), info.GetString("message"))
        {
            Source = info.GetString("source");
        }

        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("source", Source);
            info.AddValue("severity", Severity);
            info.AddValue("message", Message);
        }
    }

    /// <summary>
    /// No transport could be found that supports the required quality-of-service specifications.
    /// Unsendable may contain the list of messages.
    /// </summary>
    public class NoMatchingTransport : GTException
    {
        protected MessageDeliveryRequirements mdr;
        protected ChannelDeliveryRequirements cdr;

        public NoMatchingTransport(IConnexion connexion, MessageDeliveryRequirements mdr,
            ChannelDeliveryRequirements cdr)
            : base(Severity.Warning, String.Format("Could not find capable transport (mdr={0}, cdr={1})", mdr, cdr))
        {
            SourceComponent = connexion;
            this.mdr = mdr;
            this.cdr = cdr;
        }

        public MessageDeliveryRequirements MessageDeliveryRequirements { get { return mdr; } }
        public ChannelDeliveryRequirements ChannelDeliveryRequirements { get { return cdr; } }
    }

    /// <summary>
    /// An internal exception indicating that the connexion has been closed by the remote side.
    /// </summary>
    public class ConnexionClosedException : GTException
    {
        public ConnexionClosedException(IConnexion connexion)
            : base(Severity.Warning)
        {
            SourceComponent = connexion;
        }
    }

    /// <summary>
    /// Indicates some kind of fatal error that cannot be handled by the underlying
    /// system object.  The underlying system object is not in a useable state.
    /// Catchers have the option of restarting / reinitializing the
    /// underlying system object.
    /// </summary>
    /// <remarks>
    /// This class is serializable to support GT-Millipede,
    /// though the deserialized form may not correspond directly
    /// if some of the relevant objects are not themselves serializable.
    /// </remarks>
    [Serializable]
    public class TransportError : GTException, ISerializable
    {
        protected object transportError;

        public TransportError(object source, string message, object error)
            : base(Severity.Error, message)
        {
            SourceComponent = source;
            transportError = error;
        }

        public object ErrorObject { get { return transportError; } }

        protected TransportError(SerializationInfo info, StreamingContext context)
            : this(info.GetValue("sourcecomponent", typeof(object)), info.GetString("message"),
            info.GetValue("error", typeof(object)))
        {
            Source = info.GetString("source");
        }

        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("source", Source);
            info.AddValue("sourcecomponent", 
                SourceComponent is ISerializable ? SourceComponent : SourceComponent.ToString());
            info.AddValue("message", Message);
            info.AddValue("error", 
                ErrorObject is ISerializable ? ErrorObject : ErrorObject.ToString());
        }

    }

}
