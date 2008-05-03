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

    public class TupleMessage : Message
    {
        protected int clientId;
        protected int tupleDimension;
        protected IConvertible x, y, z;

        public TupleMessage(byte id, int clientId, IConvertible x)
            : base(id, MessageType.Tuple1D)
        {
            this.clientId = clientId;
            tupleDimension = 1;
            this.x = x;
        }

        public TupleMessage(byte id, int clientId, IConvertible x, IConvertible y)
            : base(id, MessageType.Tuple2D)
        {
            this.clientId = clientId;
            tupleDimension = 2;
            this.x = x;
            this.y = y;
        }

        public TupleMessage(byte id, int clientId, IConvertible x, IConvertible y, IConvertible z)
            : base(id, MessageType.Tuple3D)
        {
            this.clientId = clientId;
            tupleDimension = 3;
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public int ClientId { get { return clientId; } }
        public int Dimension { get { return tupleDimension; } }
        public IConvertible X { get { return x; } }
        public IConvertible Y { get { return y; } }
        public IConvertible Z { get { return z; } }
    }
}
