using System;
using System.Collections.Generic;
namespace GT
{

    /// <summary>
    /// A simple interface, similar to <c>IEnumerator</c> for processing elements sequentially
    /// but with the ability to remove the current element.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IProcessingQueue<T>
    {
        /// <summary>
        /// Advance and return the next element.
        /// </summary>
        /// <returns>the next element to be considered</returns>
        T Current { get; }

        /// <summary>
        /// Remove the current element.
        /// </summary>
        void Remove();

        bool Empty { get; }
    }

    public class SequentialListProcessor<T> : IProcessingQueue<T>
        where T: class
    {
        protected List<T> list;
        public SequentialListProcessor(List<T> list)
        {
            this.list = list;
        }

        public T Current
        {
            get { return list.Count == 0 ? null : list[0]; }
        }

        public void Remove()
        {
            list.RemoveAt(0);
        }

        public bool Empty
        {
            get { return list.Count == 0; }
        }
    }

    public class PredicateListProcessor<T> : IProcessingQueue<T>
        where T: class
    {
        protected List<T> list;
        protected T current = null;
        protected Predicate<T> predicate;

        public PredicateListProcessor(List<T> list, Predicate<T> predicate)
        {
            this.list = list;
            this.predicate = predicate;
        }

        public T Current
        {
            get
            {
                if (current != null) { return current; }
                foreach (T element in list)
                {
                    if (predicate(element))
                    {
                        return current = element;
                    }
                }
                return null;
            }
        }

        public void Remove()
        {
            list.Remove(Current);
        }

        public bool Empty
        {
            get { return Current == null; }
        }
    }

}
