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
using System.Threading;

namespace GT.Utils
{
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

    public class SingleItem<T> : IList<T>
    {
        protected T item;

        public SingleItem(T item) {
            this.item = item;
        }

        public int IndexOf(T i)
        {
            return item.Equals(i) ? 0 : -1;
        }

        public void Insert(int index, T i)
        {
            throw new NotSupportedException("SingleItem cannot grow or shrink");
        }

        public void RemoveAt(int index)
        {
            throw new NotSupportedException("SingleItem cannot grow or shrink");
        }

        public T this[int index]
        {
            get
            {
                if(index == 0) { return item; }
                throw new ArgumentOutOfRangeException("index");
            }
            set
            {
                if(index == 0) { item = value; }
                throw new ArgumentOutOfRangeException("index");
            }
        }

        public void Add(T i)
        {
            throw new NotSupportedException("SingleItem cannot grow or shrink");
        }

        public void Clear()
        {
            throw new NotSupportedException("SingleItem cannot grow or shrink");
        }

        public bool Contains(T i)
        {
            return item.Equals(i);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
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
            get { return 1; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public bool Remove(T i)
        {
            throw new NotSupportedException("SingleItem cannot grow or shrink");
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
            }

            public void Dispose()
            {
                /* nothing required */
            }

            public bool MoveNext()
            {
                // there is only one element, so once seen, we can't do more
                if (seen) { return false; }
                seen = true;
                return true;   
            }

            public void Reset()
            {
                seen = false;
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
    public class SharedQueue<T>
    {
        readonly object queueLock = new object();
        Queue<T> queue = new Queue<T>();

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
    }

}
