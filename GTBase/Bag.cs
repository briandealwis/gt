using System;
using System.Collections.Generic;
using System.Text;

namespace GT.Utils
{
    #region Utility Classes

    public class Bag<T> : ICollection<T>
    {
        protected Dictionary<T, int> contents;
        protected int totalSize;

        public Bag()
        {
            this.contents = new Dictionary<T, int>();
            totalSize = 0;
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
            foreach (T item in contents.Keys)
            {
                for (int count = contents[item]; count > 0; count--)
                {
                    array[arrayIndex++] = item;
                }
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
            return contents.Keys.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return contents.Keys.GetEnumerator();
        }

        #endregion
    }

    public class SingleItem<T> : IList<T>
    {
        protected T item;

        public SingleItem(T item) {
            this.item = item;
        }

        public int IndexOf(T item)
        {
            return item.Equals(item) ? 0 : -1;
        }

        public void Insert(int index, T item)
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

        public void Add(T item)
        {
            throw new NotSupportedException("SingleItem cannot grow or shrink");
        }

        public void Clear()
        {
            throw new NotSupportedException("SingleItem cannot grow or shrink");
        }

        public bool Contains(T item)
        {
            return item.Equals(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
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

        public bool Remove(T item)
        {
            throw new NotSupportedException("SingleItem cannot grow or shrink");
        }

        #endregion

        #region IEnumerable<T> Members

        public IEnumerator<T> GetEnumerator()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion
    }

}
