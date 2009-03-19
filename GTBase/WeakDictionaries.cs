/// The following is code posted by Nick Guerrera in 2006 to implement 
/// a weak key+weak value dictionary.
/// Source: http://blogs.msdn.com/nicholg/archive/2006/06/04/616787.aspx
using System;
using System.Collections.Generic;


namespace GT.Weak
{
    /// <summary>
    /// A dictionary whose keys are stored as a weak reference.  These
    /// dictionaries do not implement the full <see cref="IDictionary{TKey,TValue}"/>
    /// protocol as some methods will be O(n).
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class WeakKeyDictionary<TKey, TValue>
    {
        // Although we only ad WeakReference<TKey> instances, we must specify
        // the key as being of type 'object' as WeakReference<TKey> does support
        // Equals()
        private Dictionary<object, TValue> dictionary =
            new Dictionary<object, TValue>();

        /// <summary>
        /// Discard all ke-value pairs in this dictionary.
        /// </summary>
        public void Clear()
        {
            dictionary.Clear();
        }

        /// <summary>
        /// Return the number of key-value pairs contained in this
        /// dictionary.  This is an expensive operation as every
        /// pair must be checked to verify that the key has not
        /// yet been collected.
        /// </summary>
        public int Count
        {
            get {
                Flush();
                return dictionary.Count;
            }
        }

        /// <summary>
        /// Check the dictionary, discarding all key-value pairs where
        /// the key has been collected.
        /// </summary>
        public void Flush()
        {
            IList<WeakReference<TKey>> toRemove = null;
            foreach(WeakReference<TKey> wr in dictionary.Keys)
            {
                if (wr.IsAlive) { continue; }
                if (toRemove == null) { toRemove = new List<WeakReference<TKey>>(); }
                toRemove.Add(wr);
            }
            if (toRemove == null) { return; }
            foreach (WeakReference<TKey> wr in toRemove)
            {
                dictionary.Remove(wr);
            }
        }

        /// <summary>
        /// Is this instance read-only?
        /// </summary>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Check to see if <see cref="key"/> is a key in this dictionary.
        /// </summary>
        /// <param name="key">the value to check</param>
        /// <returns>true if <see cref="key"/> is a key</returns>
        public bool ContainsKey(TKey key)
        {
            return dictionary.ContainsKey(key);
        }

        /// <summary>
        /// Add the provided key-value pair.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <exception cref="System.ArgumentNullException">thrown if <see cref="key"/> 
        ///     is null.</exception>
        /// <exception cref="System.ArgumentException">thrown if anelement with the 
        ///     same key already exists in the dictionary.</exception>
        public void Add(TKey key, TValue value)
        {
            dictionary.Add(new WeakReference<TKey>(key), value);
        }

        /// <summary>
        /// Remove the value associated with the key, if present.
        /// </summary>
        /// <param name="key">the key</param>
        /// <returns>true if the value was removed, false if no associated value was found</returns>
        public bool Remove(TKey key)
        {
            return dictionary.Remove(key);
        }

        /// <summary>
        /// Try to fetch the value associated with the provided key.
        /// </summary>
        /// <param name="key">the key</param>
        /// <param name="value">the value, if found</param>
        /// <returns>true if the value was found, false if none found</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            return dictionary.TryGetValue(key, out value);
        }

        /// <summary>
        /// Return or set the value associated with <see cref="key"/>
        /// </summary>
        /// <param name="key">the key</param>
        /// <returns>the value</returns>
        /// <exception cref="KeyNotFoundException">if the key is not present</exception>
        public TValue this[TKey key]
        {
            get { return dictionary[key]; }
            set { dictionary[new WeakReference<TKey>(key)] = value; }
        }

        /// <summary>
        /// Returns a collection of the valid keys.
        /// </summary>
        public ICollection<TKey> Keys
        {
            get
            {
                IList<TKey> keys = new List<TKey>(dictionary.Count);
                IList<WeakReference<TKey>> toRemove = null;
                foreach (WeakReference<TKey> wr in dictionary.Keys)
                {
                    if (wr.IsAlive)
                    {
                        keys.Add(wr.Value);
                    } else
                    {
                        if(toRemove == null) { toRemove = new List<WeakReference<TKey>>(); }
                        toRemove.Add(wr);
                    }
                }
                if (toRemove != null)
                {
                    foreach(WeakReference<TKey> wr in toRemove)
                    {
                        dictionary.Remove(wr);
                    }
                }
                return keys;
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                Flush();
                return dictionary.Values;
            }
        }
    }

    /// <summary>
    /// A simple type-safe wrapper of <see cref="WeakReference"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class WeakReference<T> : WeakReference
    {
        protected readonly int hash;

        public WeakReference(T target)
            : base(target)
        {
            hash = target.GetHashCode();
        }

        public T Value
        {
            get { return (T)Target; }
            set { Target = value; }
        }

        public override int GetHashCode()
        {
            return hash;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) { return true; }
            // If this or obj are the same object, then both must
            // be alive.  If one is dead and the other alive, then
            // clearly they cannot represent the same object.
            if (!this.IsAlive) { return false; }
            if (obj is WeakReference)
            {
                if(!((WeakReference)obj).IsAlive) { return false; }
                return Target.Equals(((WeakReference)obj).Target);
            }
            return obj.Equals(Value);
        }
    }
}