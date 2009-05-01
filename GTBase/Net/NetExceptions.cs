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
using GT;
using System.Collections.Generic;

namespace GT.Net
{
    public class CannotSendMessagesError : GTCompositeException
    {
        protected IDictionary<Exception, IList<PendingMessage>> messages;

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
            if (messages != null && messages.Count > 0)
            {
                throw this;
            }
        }
    }

    public class CannotConnectException : GTException
    {
        public CannotConnectException(string m)
            : base(Severity.Error, m)
        { }

        public CannotConnectException(string m, Exception e)
            : base(Severity.Error, m, e)
        { }

        // "There was a problem connecting to the server you specified. " +
        //"The address or port you provided may be improper, the receiving server may be down, " +
        //"full, or unavailable, or your system's host file may be corrupted. " +
        //"See inner exception for details."
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
    public class TransportError : GTException
    {
        protected object transportError;

        public TransportError(object source, string message, object error)
            : base(Severity.Error, message)
        {
            SourceComponent = source;
            transportError = error;
        }

        public object ErrorObject { get { return transportError; } }
    }

    public class TransportBackloggedWarning : GTException
    {
        public TransportBackloggedWarning(ITransport t) 
            : this(Severity.Information, t)
        {}

        public TransportBackloggedWarning(Severity sev, ITransport t)
            : base(sev)
        {
            SourceComponent = t;
        }
    }

}
