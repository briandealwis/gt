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
namespace GT.Net
{

    /// <summary>Represents a 1-tuple.</summary>
    /// <typeparam name="T_X">The type of the tuple parameter X.</typeparam>
    public class RemoteTuple<T_X>
    {
        /// <summary>The value of this tuple.</summary>
        protected T_X x;
        /// <summary>A value of this tuple.</summary>
        public T_X X { get { return x; } set { x = value; } }
        /// <summary>Constructor.</summary>
        public RemoteTuple() { }
        /// <summary>Constructor.</summary>
        public RemoteTuple(T_X x)
        {
            this.x = x;
        }
    }

    /// <summary>Represents a 2-tuple.</summary>
    /// <typeparam name="T_X">The type of the tuple parameter X.</typeparam>
    /// <typeparam name="T_Y">The type of the tuple parameter Y.</typeparam>
    public class RemoteTuple<T_X, T_Y>
    {
        /// <summary>A value of this tuple.</summary>
        protected T_X x;
        /// <summary>A value of this tuple.</summary>
        public T_X X { get { return x; } set { x = value; } }
        /// <summary>A value of this tuple.</summary>
        protected T_Y y;
        /// <summary>A value of this tuple.</summary>
        public T_Y Y { get { return y; } set { y = value; } }
        /// <summary>Constructor.</summary>
        public RemoteTuple() { }
        /// <summary>Constructor.</summary>
        public RemoteTuple(T_X x, T_Y y)
        {

            this.x = x;
            this.y = y;
        }
    }

    /// <summary>Represents a 3-tuple.</summary>
    /// <typeparam name="T_X">The type of the tuple parameter X.</typeparam>
    /// <typeparam name="T_Y">The type of the tuple parameter Y.</typeparam>
    /// <typeparam name="T_Z">The type of the tuple parameter Z.</typeparam>
    public class RemoteTuple<T_X, T_Y, T_Z>
    {
        /// <summary>A value of this tuple.</summary>
        protected T_X x;
        /// <summary>A value of this tuple.</summary>
        public T_X X { get { return x; } set { x = value; } }
        /// <summary>A value of this tuple.</summary>
        protected T_Y y;
        /// <summary>A value of this tuple.</summary>
        public T_Y Y { get { return y; } set { y = value; } }
        /// <summary>A value of this tuple.</summary>
        protected T_Z z;
        /// <summary>A value of this tuple.</summary>
        public T_Z Z { get { return z; } set { z = value; } }
        /// <summary>Constructor.</summary>
        public RemoteTuple() { }
        /// <summary>Constructor.</summary>
        public RemoteTuple(T_X x, T_Y y, T_Z z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    /// <summary>
    /// A message containing a revised tuple from a particular client.
    /// </summary>
    public class TupleMessage : Message
    {
        /// <summary>
        /// Encode a 1D tuple
        /// </summary>
        /// <param name="channelId">the channel</param>
        /// <param name="clientId">the client sending the tuple</param>
        /// <param name="x">the tuple value</param>
        public TupleMessage(byte channelId, int clientId, IConvertible x)
            : base(channelId, MessageType.Tuple1D)
        {
            ClientId = clientId;
            Dimension = 1;
            X = x;
        }

        /// <summary>
        /// Encode a 2D tuple
        /// </summary>
        /// <param name="channelId">the channel</param>
        /// <param name="clientId">the client sending the tuple</param>
        /// <param name="x">the first tuple component</param>
        /// <param name="y">the second tuple component</param>
        public TupleMessage(byte channelId, int clientId, IConvertible x, IConvertible y)
            : base(channelId, MessageType.Tuple2D)
        {
            ClientId = clientId;
            Dimension = 2;
            X = x;
            Y = y;
        }

        /// <summary>
        /// Encode a 3D tuple
        /// </summary>
        /// <param name="channelId">the channel</param>
        /// <param name="clientId">the client sending the tuple</param>
        /// <param name="x">the first tuple component</param>
        /// <param name="y">the second tuple component</param>
        /// <param name="z">the third tuple component</param>
        public TupleMessage(byte channelId, int clientId, IConvertible x, IConvertible y, IConvertible z)
            : base(channelId, MessageType.Tuple3D)
        {
            ClientId = clientId;
            Dimension = 3;
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Return the id of the client that sent this tuple
        /// </summary>
        public int ClientId { get; protected set; }

        /// <summary>
        /// Return the number of components in this tuple
        /// </summary>
        public int Dimension { get; protected set; }

        /// <summary>
        /// Return the tuple's X component
        /// </summary>
        public IConvertible X { get; protected set; }

        /// <summary>
        /// Return the tuple's Y component
        /// </summary>
        public IConvertible Y { get; protected set; }

        /// <summary>
        /// Return the tuple's Z component
        /// </summary>
        public IConvertible Z { get; protected set; }
    }
}
