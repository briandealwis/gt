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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace GT.Utils
{
    /// <summary>
    /// A bag, similar to a set but where a count is maintained for each item
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Bag<T> : ICollection<T>
    {
        protected Dictionary<T, int> contents;
        protected int totalSize;

        public Bag()
        {
            contents = new Dictionary<T, int>();
            totalSize = 0;
        }

        public int Occurrences(T key)
        {
            int count;
            if(!contents.TryGetValue(key, out count)) { return 0; }
            return count;
        }

        #region ICollection<T> Members

        public void Add(T item)
        {
            int count;
            if (!contents.TryGetValue(item, out count))
            {
                count = 0;
            }
            contents[item] = ++count;
            totalSize++;
        }

        public void Clear()
        {
            contents.Clear();
            totalSize = 0;
        }

        public bool Contains(T item)
        {
            return contents.ContainsKey(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if(array == null) { throw new ArgumentNullException("array"); }
            if(arrayIndex < 0) { throw new ArgumentOutOfRangeException("arrayIndex"); }
            if(array.Length - arrayIndex < Count)
            {
                throw new ArgumentException("array is too small", "array");
            }
            foreach (T item in this)
            {
                array[arrayIndex++] = item;
            }
        }

        public int Count
        {
            get { return totalSize; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(T item)
        {
            int count;
            if (!contents.TryGetValue(item, out count))
            {
                return false;
            }
            totalSize--;
            if (count > 1)
            {
                contents[item] = count - 1;
            } else {
                contents.Remove(item);
            }
            return true;
        }

        #endregion

        #region IEnumerable<T> Members

        public IEnumerator<T> GetEnumerator()
        {
            return new BagEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new BagEnumerator(this);
        }

        private class BagEnumerator : IEnumerator<T>
        {
            private Bag<T> bag;
            private IEnumerator<T> keys;
            private int count = -1;

            internal BagEnumerator(Bag<T> ts)
            {
                bag = ts;
                Reset();
            }

            public bool MoveNext()
            {
                if (--count > 0) { return true; }
                if(!keys.MoveNext()) { return false; }
                count = bag.Occurrences(keys.Current);
                return true;
            }

            public void Reset()
            {
                keys = bag.contents.Keys.GetEnumerator();
                count = -1;
            }

            public T Current
            {
                get { return keys.Current; }
            }

            object IEnumerator.Current
            {
                get { return keys.Current; }
            }

            public void Dispose()
            {
                keys.Dispose();
            }
        }
        #endregion
    }

    /// <summary>
    /// A compact list intended for lists containing at most a single item.
    /// </summary>
    /// <typeparam name="T">the type of the objects making up this collection's contents</typeparam>
    public class SingleItem<T> : IList<T>
    {
        protected T item;
        protected bool hasItem;

        public SingleItem()
        {
            item = default(T);
            hasItem = false;
        }

        public SingleItem(T item) 
        {
            this.item = item;
            hasItem = true;
        }

        public int IndexOf(T i)
        {
            if (!hasItem) { return -1; }
            return item.Equals(i) ? 0 : -1;
        }

        public void Insert(int index, T i)
        {
            if (hasItem) { throw new NotSupportedException("SingleItem cannot grow or shrink"); }
            if (index != 0) { throw new ArgumentOutOfRangeException(); }
            item = i;
            hasItem = true;
        }

        public void RemoveAt(int index)
        {
            if (!hasItem) { throw new NotSupportedException("SingleItem cannot grow or shrink"); }
            if (index != 0) { throw new ArgumentOutOfRangeException(); }
            Clear();
        }

        public T this[int index]
        {
            get
            {
                if (hasItem && index == 0) { return item; }
                throw new ArgumentOutOfRangeException("index");
            }
            set
            {
                if (index == 0) { hasItem = true;  item = value; }
                else { throw new ArgumentOutOfRangeException("index"); }
            }
        }

        public void Add(T i)
        {
            if (hasItem) { throw new NotSupportedException("SingleItem cannot grow or shrink"); }
            item = i;
            hasItem = true;
        }

        public void Clear()
        {
            item = default(T);
            hasItem = false;
        }

        public bool Contains(T i)
        {
            return hasItem && item.Equals(i);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (!hasItem) { return; }
            if (array == null) { throw new ArgumentNullException("array"); }
            if (arrayIndex < 0) { throw new ArgumentOutOfRangeException("arrayIndex"); }
            if (arrayIndex >= array.Length)
            {
                throw new ArgumentException(
                    "arrayIndex is equal to or greater than the length of array", "arrayIndex");
            }
            array[arrayIndex] = item;
        }

        public int Count
        {
            get { return hasItem ? 1 : 0; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public bool Remove(T i)
        {
            if (!hasItem || !i.Equals(item)) { return false; }
            Clear();
            return true;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new SingleItemEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new SingleItemEnumerator(this);
        }

        private class SingleItemEnumerator : IEnumerator<T>
        {
            bool seen = false;
            private readonly SingleItem<T> si;

            internal SingleItemEnumerator(SingleItem<T> si)
            {
                this.si = si;
                Reset();
            }

            public void Dispose()
            {
                /* nothing required */
            }

            public bool MoveNext()
            {
                // there is at most one element, so once seen, we can't do more
                if (seen) { return false; }
                seen = true;
                return true;   
            }

            public void Reset()
            {
                seen = si.Count == 0;
            }

            public T Current
            {
                get { return si[0]; }
            }

            object IEnumerator.Current
            {
                get { return si[0]; }
            }
        }

    }


    /// <summary>
    /// A thread-safe shared queue.  Inpired by various sources on the
    /// internet.
    /// </summary>
    /// <typeparam name="T">the type of the objects making up this collection's contents</typeparam>
    public class SharedQueue<T>
    {
        readonly object queueLock = new object();
        protected Queue<T> queue = new Queue<T>();

        public void Enqueue(T o)
        {
            lock (queueLock)
            {
                queue.Enqueue(o);

                // We always need to pulse, even if the queue wasn't
                // empty before. Otherwise, if we add several items
                // in quick succession, we may only pulse once, waking
                // a single thread up, even if there are multiple
                // threads waiting for items.            
                Monitor.Pulse(queueLock);
            }
        }

        public T Dequeue()
        {
            lock (queueLock)
            {
                // If the queue is empty, wait for an item to be added
                // Note that this is a while loop, as we may be pulsed
                // but not actually run before another thread has come in and
                // consumed the newly added object. In such a case, we
                // need to wait again for another pulse.
                while (queue.Count == 0)
                {
                    // This releases queueLock, only reacquiring it
                    // after being woken up by a call to Pulse
                    Monitor.Wait(queueLock);
                }
                return queue.Dequeue();
            }
        }

        /// <summary>
        /// Try to dequeue an object.
        /// </summary>
        /// <param name="value">the dequeued result, or the default of <typeparamref name="T"/>
        /// if the timeout expires.</param>
        /// <returns>true if a value was dequeued, or false if there was nothing available</returns>
        public bool TryDequeue(out T value)
        {
            lock (queueLock)
            {
                if(queue.Count > 0)
                {
                    value = queue.Dequeue();
                    return true;
                }
                value = default(T);
                return false;
            }
        }

        /// <summary>
        /// Return the number of objects available in this queue.
        /// </summary>
        public int Count
        {
            get
            {
                lock (queueLock)
                {
                    return queue.Count;
                }
            }
        }

        public override string ToString()
        {
            StringBuilder result = new StringBuilder("[");
            int count = 0;
            foreach (object item in queue)
            {
                result.Append(item.ToString());
                result.Append(", ");
                count++;
            }
            if (count > 0)
            {
                result.Remove(result.Length - 2, 2);
            }
            result.Append("]");
            return result.ToString();
        }
    }

    /// <summary>
    /// A simple class for maintaining an ordered set of items as ordered
    /// by time of first entry.  New items that are already present are ignored.
    /// New items that are not present are added to the end of the set.
    /// </summary>
    /// <typeparam name="T">the type of the objects making up this collection's contents</typeparam>
    public class SequentialSet<T> : IEnumerable<T>
    {
        // We use a dictionary for fast is-included tests, and a list to maintain
        // the list order
        protected IDictionary<T, T> containedSet = new Dictionary<T, T>();
        protected IList<T> elements = new List<T>();

        ///<summary>
        /// Create a sequential set with the provided items in their presented order.
        ///</summary>
        public SequentialSet(IList<T> items)
        {
            foreach (T item in items)
            {
                Add(item);
            }
        }

        ///<summary>
        /// Create an empty sequential set
        ///</summary>
        public SequentialSet() { }

        public int Count
        {
            get { return elements.Count; }
        }

        /// <summary>
        /// Returns the element at the provided index.
        /// </summary>
        /// <param name="index">the element position</param>
        /// <returns>the element at the provided index</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">index is not a 
        /// valid index in the set</exception>
        public T this[int index] { get { return elements[index]; } }

        /// <summary>
        /// Add the provided item to the set.
        /// </summary>
        /// <param name="item">the item to be added</param>
        /// <returns>true if the item was newly added, or false if the item
        ///    was already part of the set</returns>
        public bool Add(T item)
        {
            if (containedSet.ContainsKey(item))
            {
                return false;
            }
            containedSet[item] = item;
            elements.Add(item);
            return true;
        }

        ///<summary>
        /// Remove the provided item, if present.
        ///</summary>
        ///<param name="item">the item to be removed</param>
        ///<returns>true if present, false otherwise</returns>
        public bool Remove(T item)
        {
            if (!containedSet.Remove(item)) { return false; }
            elements.Remove(item);
            return true;
        }

        ///<summary>
        ///Return true if the provided item is part of this set.
        ///</summary>
        ///<param name="item">the item to be checked</param>
        ///<returns>true if the provided item is part of the set, false otherwise</returns>
        public bool Contains(T item)
        {
            return containedSet.ContainsKey(item);
        }

        /// <summary>
        ///Return an enumerator for the items that maintains the item order.
        /// </summary>
        /// <returns>an enumerator</returns>
        public IEnumerator<T> GetEnumerator()
        {
            return elements.GetEnumerator();
        }

        /// <summary>
        ///Return an enumerator for the items that maintains the item order.
        /// </summary>
        /// <returns>an enumerator</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return elements.GetEnumerator();
        }

        /// <summary>
        /// Add the items in the provided enumerable.
        /// </summary>
        /// <param name="collection">the items to be added</param>
        public void AddAll(IEnumerable<T> collection)
        {
            foreach (T item in collection)
            {
                Add(item);
            }
        }
    }

    #region Weak Collections

    /// <summary>
    /// A dictionary whose keys are stored as a weak reference.  These
    /// dictionaries do not implement the full <see cref="IDictionary{TKey,TValue}"/>
    /// protocol as some methods will be O(n).
    /// </summary>
    /// <typeparam name="TKey">the type of the objects making up this collection's keys</typeparam>
    /// <typeparam name="TValue">the type of the objects making up this collection's values</typeparam>
    public class WeakKeyDictionary<TKey, TValue>
    {
        // Although we only add WeakReference<TKey> instances, we must specify
        // the key as being of type 'object' as WeakReference<TKey> does support
        // Equals(), enabling cheap .Contains() tests without creating garbage.
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
            get
            {
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
            foreach (WeakReference<TKey> wr in dictionary.Keys)
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
        public IEnumerable<TKey> Keys
        {
            get
            {
                IList<WeakReference<TKey>> toRemove = null;
                foreach (WeakReference<TKey> wr in dictionary.Keys)
                {
                    if (wr.IsAlive)
                    {
                        yield return wr.Value;
                    }
                    else
                    {
                        if (toRemove == null) { toRemove = new List<WeakReference<TKey>>(); }
                        toRemove.Add(wr);
                    }
                }
                if (toRemove != null)
                {
                    foreach (WeakReference<TKey> wr in toRemove)
                    {
                        dictionary.Remove(wr);
                    }
                }
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
    /// A set whose elements are stored as a weak reference.
    /// </summary>
    /// <typeparam name="T">the type of the objects making up this collection</typeparam>
    public class WeakCollection<T> : ICollection<T>
    {
        protected IList<WeakReference<T>> sublist = new List<WeakReference<T>>();

        public IEnumerator<T> GetEnumerator()
        {
            foreach (WeakReference<T> wr in sublist)
            {
                if(wr.IsAlive)
                {
                    yield return wr.Value;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(T item)
        {
            for (int i = 0; i < sublist.Count; i++)
            {
                if(!sublist[i].IsAlive)
                {
                    sublist[i] = new WeakReference<T>(item);
                    return;
                }
            }
            sublist.Add(new WeakReference<T>(item));
        }

        public void Clear()
        {
            sublist.Clear();
        }

        public bool Contains(T item)
        {
            foreach (WeakReference<T> wr in sublist)
            {
                if (wr.IsAlive && wr.Value.Equals(item))
                {
                    return true;
                }
            }
            return false;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            foreach (WeakReference<T> wr in sublist)
            {
                if (wr.IsAlive)
                {
                    array[arrayIndex++] = wr.Value;
                }
            }
        }

        public bool Remove(T item)
        {
            for(int i = 0; i < sublist.Count; i++)
            {
                if(sublist[i].IsAlive && sublist[i].Value.Equals(item))
                {
                    sublist.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Return the number of still-alive target objects in the collection
        /// </summary>
        public int Count
        {
            get
            {
                int n = 0;
                foreach (WeakReference<T> element in sublist)
                {
                    if (element.IsAlive) { n++; }
                }
                return n;
            }
        }

        public bool IsReadOnly { get { return false; } }
    }

    /// <summary>
    /// A simple type-safe wrapper of <see cref="WeakReference"/>
    /// </summary>
    /// <typeparam name="T">the type of the target object</typeparam>
    public class WeakReference<T> : WeakReference
    {
        /// <summary>
        /// The hash value for the target (stored in case the target disappears)
        /// </summary>
        protected readonly int hash;

        /// <summary>
        /// Create a new type-safe weak reference object
        /// </summary>
        /// <param name="target">the referenced target object</param>
        public WeakReference(T target)
            : base(target)
        {
            hash = target.GetHashCode();
        }

        /// <summary>
        /// Get/set the value of the weak reference.
        /// </summary>
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
                if (!((WeakReference)obj).IsAlive) { return false; }
                return Target.Equals(((WeakReference)obj).Target);
            }
            return obj.Equals(Value);
        }
    }

    #endregion

    /// <summary>
    /// A simple delay queue similar in spirit to the java.util.concurrent.DelayQueue class.  
    /// Elements are inserted with some delay count using
    /// <see cref="Enqueue"/>.  The delay counts are represented as the relative number 
    /// of unitless ticks (e.g., could correspond to milliseconds).  Elements can only be
    /// dequeued when their delay has expired.  Time passing is indicated by periodically
    /// calling <see cref="Dequeue"/> with the number of ticks that have elapsed since
    /// the last call; any elements whose delay has expired are then dequeued and
    /// reported.
    /// </summary>
    /// <typeparam name="T">the type of elements</typeparam>
    public class DelayQueue<T>
    {
        /// <summary>
        /// DelayNode captures the necessary information in the delay
        /// list.  The delay list is sorted by time remaining; each node
        /// contains a delta of the number of additional ticks relative to
        /// its parent.
        /// </summary>
        protected class DelayNode
        {
            internal uint delta;
            internal T element;
            internal DelayNode next;

            /// <summary>
            /// Clear the contents of this node
            /// </summary>
            public void Clear()
            {
                delta = 0;
                element = default(T);
                next = null;
            }
        }

        /// <summary>
        /// We use a managed pool to minimize memory overhead
        /// </summary>
        protected Pool<DelayNode> nodePool = new ManagedPool<DelayNode>(0, 5,
            () => new DelayNode(), dn => dn.Clear(), dn => dn.Clear());

        /// <summary>
        /// the first element in the delay queue: this is the node that
        /// will expire earliest.
        /// </summary>
        protected DelayNode first = null;

        /// <summary>
        /// The number of elements in this queue
        /// </summary>
        public uint Count { get; protected set; }

        /// <summary>
        /// The maximum delay for any element in this queue.
        /// </summary>
        public uint MaximumDelay { get; protected set; }

        /// <summary>
        /// Create a new delay queue instance
        /// </summary>
        public DelayQueue()
        {
            Count = 0;
            MaximumDelay = 0;
        }

        /// <summary>
        /// Enqueue the given element for at least <see cref="delay"/> ticks.
        /// </summary>
        /// <param name="element">the element to be enqueued</param>
        /// <param name="delay">the number of ticks the element must be kept for</param>
        public void Enqueue(T element, uint delay)
        {
            DelayNode newNode = nodePool.Obtain();
            Debug.Assert(newNode.next == null);
            newNode.element = element;

            Count++;
            // Handle special case where this element should be the first...
            if (first == null) {
                MaximumDelay = newNode.delta = delay;
                first = newNode;
                return;
            }
            if (delay < first.delta)
            {
                // MaximumDelay doesn't change
                newNode.delta = delay;
                newNode.next = first;
                first.delta -= delay;
                first = newNode;
                return;
            }

            Debug.Assert(first.delta <= delay);
            // Find the right position for the element in the list and adjust
            // the subsequent element's delta appropriately
            delay -= first.delta;
            DelayNode parent = first;
            while (parent.next != null && parent.next.delta <= delay)
            {
                parent = parent.next;
                delay -= parent.delta;
            }
            // MaximumDelay needs only be updated if newNode is the new last node
            if (parent.next == null)
            {
                MaximumDelay += delay;
            }
            newNode.delta = delay;
            newNode.next = parent.next;
            parent.next = newNode;
            // and adjust the next node's delta relative newNode
            if (newNode.next != null)
            {
                // should be > since if we're equal, then newNode should go
                // after it (viz. the condition above)
                Debug.Assert(newNode.next.delta > delay);
                newNode.next.delta -= delay;
            }
        }

        /// <summary>
        /// Notify that <see cref="elapsed"/> ticks have elapsed.
        /// Dequeue any items whose delays have expired, triggering
        /// the <see cref="dequeued"/> delegate.
        /// </summary>
        /// <param name="elapsed">the number of ticks elapsed since the last call</param>
        /// <param name="dequeued">a delegate triggered with all elements whose delay 
        /// has expired</param>
        /// <returns>true if some elements expired, false otherwise</returns>
        public bool Dequeue(uint elapsed, Action<T> dequeued)
        {
            bool hasDequeued = false;
            while (first != null && first.delta <= elapsed)
            {
                dequeued(first.element);
                hasDequeued = true;
                Count--;
                MaximumDelay -= first.delta;
                elapsed -= first.delta;
                DelayNode scrap = first;
                first = first.next;
                nodePool.Return(scrap);
            }
            if (first != null)
            {
                Debug.Assert(elapsed < first.delta);
                MaximumDelay -= elapsed;
                first.delta -= elapsed;
            }
            return hasDequeued;
        }
    }

}
