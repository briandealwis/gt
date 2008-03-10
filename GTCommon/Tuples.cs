namespace GT.Net
{

    /// <summary>Represents a 1-tuple.</summary>
    /// <typeparam name="T">The type of the tuple parameter T.</typeparam>
    public class RemoteTuple<T>
    {
        /// <summary>The value of this tuple.</summary>
        protected T x;
        /// <summary>A value of this tuple.</summary>
        public T X { get { return x; } set { x = value; } }
        /// <summary>Constructor.</summary>
        public RemoteTuple() { }
        /// <summary>Constructor.</summary>
        public RemoteTuple(T x)
        {
            this.x = x;
        }
    }

    /// <summary>Represents a 2-tuple.</summary>
    /// <typeparam name="T">The type of the tuple parameter T.</typeparam>
    /// <typeparam name="K">The type of the tuple parameter T.</typeparam>
    public class RemoteTuple<T, K>
    {
        /// <summary>A value of this tuple.</summary>
        protected T x;
        /// <summary>A value of this tuple.</summary>
        public T X { get { return x; } set { x = value; } }
        /// <summary>A value of this tuple.</summary>
        protected K y;
        /// <summary>A value of this tuple.</summary>
        public K Y { get { return y; } set { y = value; } }
        /// <summary>Constructor.</summary>
        public RemoteTuple() { }
        /// <summary>Constructor.</summary>
        public RemoteTuple(T x, K y)
        {

            this.x = x;
            this.y = y;
        }
    }

    /// <summary>Represents a 3-tuple.</summary>
    /// <typeparam name="T">The type of the tuple parameter T.</typeparam>
    /// <typeparam name="K">The type of the tuple parameter K.</typeparam>
    /// <typeparam name="J">The type of the tuple parameter J.</typeparam>
    public class RemoteTuple<T, K, J>
    {
        /// <summary>A value of this tuple.</summary>
        protected T x;
        /// <summary>A value of this tuple.</summary>
        public T X { get { return x; } set { x = value; } }
        /// <summary>A value of this tuple.</summary>
        protected K y;
        /// <summary>A value of this tuple.</summary>
        public K Y { get { return y; } set { y = value; } }
        /// <summary>A value of this tuple.</summary>
        protected J z;
        /// <summary>A value of this tuple.</summary>
        public J Z { get { return z; } set { z = value; } }
        /// <summary>Constructor.</summary>
        public RemoteTuple() { }
        /// <summary>Constructor.</summary>
        public RemoteTuple(T x, K y, J z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }
}
