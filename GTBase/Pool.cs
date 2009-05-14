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
using System.Text;
using System.Threading;

namespace GT.Utils 
{
    /// <summary>
    /// A delegate representing a method returning an instance of some type;
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public delegate T Returning<T>();

    /// <summary>
    /// A simple object pool for allocating and reusing objects.
    /// The pool maintains a minimum of <see cref="Low"/> objects.
    /// If <see cref="Strict"/>, then the pool will only allow a
    /// maximum of <see cref="High"/> objects to be allocated; if
    /// false, then more are created upon demand.  Objects are
    /// requested from the pool by <see cref="Obtain"/>, and returned
    /// once finished with via <see cref="Return"/>; if a
    /// pool object has become ruined, it should be marked as
    /// such by calling <see cref="Ruined"/>.
    /// 
    /// <para>
    /// Simple pools differ from managed pools in that the 
    /// allocated pool elements are not explicitly tracked, and are
    /// not required to be returned to the pool.  Managed pools
    /// (such as <see cref="ManagedPool{T}"/> and <see cref="StrictPool{T}"/>)
    /// differ in that they require the allocated pool elements
    /// to be returned through <see cref="Return"/> or <see cref="Ruined"/>.
    /// </para>
    /// </summary>
    /// <typeparam name="T">the type of object</typeparam>
    public class Pool<T> : IDisposable
    {
        protected object lockObject = new object();
        protected Queue<T> available = new Queue<T>();
        protected Returning<T> createBlock;
        protected Action<T> rehabBlock;
        protected Action<T> destroyBlock;
        protected int allocated = 0;

        /// <summary>
        /// The low water mark: this number is the minimum total number 
        /// of elements that should be managed by this pool.
        /// </summary>
        public uint Low { get; protected set; }

        /// <summary>
        /// The high water mark: this number is the maximum total number 
        /// of elements that should be managed by this pool.  If <see cref="Strict"/>
        /// is true, then this limit is strongly enforced; if false, then
        /// elements are created as demand warrants, but excess elements
        /// are destroyed once returned.
        /// </summary>
        public uint High { get; protected set; }

        /// <summary>
        /// If true, then this pool strongly enforces the minimum and maximum
        /// values (<see cref="Low"/> and <see cref="High"/>).
        /// </summary>
        public virtual bool Strict { get { return false; } }

        /// <summary>
        /// Create a new managed object pool.  There should be a minimum of <see cref="low"/>
        /// and a maximum of <see cref="high"/> elements maintained by the pool.
        /// If <see cref="Strict"/>, then these limits are strongly enforced;
        /// if not strict, then we create new elements as needed, though
        /// ensuring that excess elements are destroyed once returned.
        /// </summary>
        /// <param name="low">minimum number of instances to keep around</param>
        /// <param name="high">maximum number of instances to keep around</param>
        /// <param name="createBlock">delegate for creating new instances</param>
        /// <param name="rehabBlock">delegate for rehabilitating returned instances; can be null</param>
        /// <param name="destroyBlock">delegate for properly tearing down superfluous instances; can be null</param>
        public Pool(uint low, uint high, Returning<T> createBlock, Action<T> rehabBlock,
            Action<T> destroyBlock)
        {
            Low = low;
            High = high;

            if(createBlock == null) { throw new ArgumentNullException("createBlock"); }
            this.createBlock = createBlock;
            this.destroyBlock = destroyBlock;
            this.rehabBlock = rehabBlock;
        }

        /// <summary>
        /// Return true if this pool has elements available to be obtained
        /// without blocking.
        /// </summary>
        public virtual bool Available
        {
            get { return true; }
        }

        /// <summary>
        /// Dispose of this instance and destroy any elements managed by this pool.
        /// </summary>
        public virtual void Dispose()
        {
            Queue<T> originalIn = available;
            available = null;
            while (originalIn.Count > 0)
            {
                DestroyElement(originalIn.Dequeue());
            }
        }

        /// <summary>
        /// Make a replica of the receiver with its current settings.
        /// This does not copy the created elements associated with
        /// this instance. The copy will not be in a started state
        /// regardless of whether the receiver is started.
        /// </summary>
        public virtual Pool<T> Copy()
        {
            return new Pool<T>(Low, High, createBlock, rehabBlock, destroyBlock);
        }

        /// <summary>
        /// Obtain an element from the pool.  If there are no pool elements
        /// available, then a new element is created; this behaviour may be
        /// different in subclasses.
        /// Callers are requested to return the element when finished via
        /// <see cref="Return"/> or <see cref="Ruined"/>.
        /// <summary>
        /// <returns>an element from the pool</returns>
        public virtual T Obtain()
        {
            T poolElement;
            lock (this)
            {
                poolElement = available.Count > 0 ? available.Dequeue() : CreateElement();
            }
            return poolElement;
        }

        /// <summary>
        /// Try to obtain an element from the pool if there are any available.
        /// Return <c>default(T)</c> if there are no pool elements immediately
        /// available.  This call should not block.
        /// </summary>
        /// <returns>a managed pool element, or <c>default(T)</c> if there are
        ///     no pool elements available at this moment.</returns>
        public virtual T TryObtain()
        {
            if (!Available) { return default(T); }
            return Obtain();
        }

        /// <summary>
        /// Return a previously-obtained pool element to the pool.
        /// </summary>
        /// <param name="poolElement">the element to return</param>
        /// <exception cref="ArgumentException">thrown if the provided
        /// pool element was not actually managed by this pool.</exception>
        public virtual void Return(T poolElement)
        {
            lock (lockObject)
            {
                // We se <= as the assumption is that poolElement is one of our elements, and so 
                // will be transitioning from the <assigned> list to the <available> list
                if (this.Count <= High)
                {
                    RehabilitateElement(poolElement);
                    available.Enqueue(poolElement);
                }
                else
                {
                    DestroyElement(poolElement);
                }
            }
        }
        
        /// <summary>
        /// Return a previously-obtained element to the pool, but
        /// noting that the element has been ruined in some form
        /// and is not suitable for reuse; the element will be destroyed.
        /// </summary>
        /// <param name="poolElement">the ruined element</param>
        /// <exception cref="ArgumentException">thrown if the provided
        /// pool element was not actually managed by this pool.</exception>
        public virtual void Ruined(T poolElement)
        {
            lock (lockObject)
            {
                DestroyElement(poolElement);
                if (this.Count < High)
                {
                    available.Enqueue(CreateElement());
                }
            }
        }

        /// <summary>
        /// Return the number of elements managed by this pool.
        /// </summary>
        public virtual int Count
        {
            get { return allocated; }
        }

        /// <summary>
        /// Return the number of outstanding elements managed by this pool.
        /// </summary>
        public virtual int Out
        {
            get { return allocated - (available == null ? 0 : available.Count); }
        }

        protected T CreateElement()
        {
            allocated++;
            return createBlock();
        }

        protected void RehabilitateElement(T element)
        {
            if (rehabBlock == null) { return; }
            rehabBlock(element);
        }

        protected void DestroyElement(T element)
        {
            allocated--;
            if (destroyBlock == null) { return; }
            destroyBlock(element);
        }

        public override string ToString()
        {
            return String.Format("{0}<{1}>: in={2}",
                GetType().Name, typeof(T).Name, ToString(available));
        }

        /// <summary>
        /// Helper method for pretty-printing
        /// </summary>
        /// <param name="collection">the collection to pretty-print</param>
        /// <returns>the result</returns>
        public static string ToString(IEnumerable collection)
        {
            StringBuilder result = new StringBuilder("[");
            int count = 0;
            foreach (object item in collection)
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
    /// A managed pool build on a simple pool by also tracking
    /// the allocated objects.
    /// Unlike <see cref="Pool{T}">simple pools</see>, pool elements allocated
    /// from a managed pool are explicitly tracked, and are
    /// required to be returned to the pool through <see cref="Return"/> 
    /// or <see cref="Ruined"/>.  Instances that are not returned are
    /// lost which is not a good thing!
    /// </summary>
    /// <seealso cref="Pool{T}"/>
    /// <typeparam name="T">the object type managed by this pool</typeparam>
    public class ManagedPool<T> : Pool<T>
    {
        protected Dictionary<T, T> assigned = new Dictionary<T, T>();

        public ManagedPool(uint low, uint high, Returning<T> createBlock,
            Action<T> rehabBlock, Action<T> destroyBlock)
            : base(low, high, createBlock, rehabBlock, destroyBlock)
        {
        }

        /// <summary>
        /// Obtain an element from the pool.  If there are no pool elements
        /// available, then a new element is created; this behaviour may be
        /// different in subclasses.
        /// Callers are *required* to return the element when finished via
        /// <see cref="Return"/> or <see cref="Ruined"/>.
        /// <summary>
        /// <returns>an element from the pool</returns>
        public override T Obtain()
        {
            lock(lockObject)
            {
                T poolElement = base.Obtain();
                assigned.Add(poolElement, poolElement);
                return poolElement;
            }
        }

        public override void Return(T poolElement)
        {
            lock(lockObject)
            {
                if(!assigned.Remove(poolElement))
                {
                    throw new ArgumentException("not allowed to add arbitrary objects to a pool!");
                }
                base.Return(poolElement);
            }
        }

        public override void Ruined(T poolElement)
        {
            lock(lockObject)
            {
                if(!assigned.Remove(poolElement))
                {
                    throw new ArgumentException("not allowed to add arbitrary objects to a pool!");
                }
                base.Ruined(poolElement);
            }
        }

        public override void Dispose()
        {
            Dictionary<T, T> originalOut = assigned;
            assigned = null;
            base.Dispose();
            foreach(T element in originalOut.Keys)
            {
                DestroyElement(element);
            }
        }

        public override string ToString()
        {
            return String.Format("{0}<{1}>: in={2} out={3}",
                GetType().Name, typeof(T).Name, available, ToString(assigned.Keys));
        }
    }

    /// <summary>
    /// A strict pool is a managed pool that strictly enforces the 
    /// high- and low-water marks.  A requestor is blocked 
    /// if there are no elements available.
    /// As with a <see cref="ManagedPool{T}">managed pool</see>, a
    /// strict pool also explicitly tracks the pool elements allocated,
    /// and are  required to be returned to the pool through <see cref="Return"/> 
    /// or <see cref="Ruined"/>.  Instances that are not returned are
    /// lost which is not a good thing!
    /// </summary>
    /// <seealso cref="Pool{T}"/>
    /// <seealso cref="ManagedPool{T}"/>
    /// <typeparam name="T">the object type managed by this pool</typeparam>
    public class StrictPool<T> : ManagedPool<T>
    {
        public override bool Strict { get { return true; } }

        /// <summary>
        /// Create a new strict managed object pool.  There should be a minimum of 
        /// <see cref="low"/> and a maximum of <see cref="high"/> elements maintained 
        /// by the pool. These limits are strongly enforced by this pool, such that
        /// requestors are blocked on <see cref="Obtain"/> if there are no elements available.
        /// </summary>
        /// <param name="low">minimum number of instances to keep around</param>
        /// <param name="high">maximum number of instances to keep around</param>
        /// <param name="createBlock">delegate for creating new instances</param>
        /// <param name="rehabBlock">delegate for rehabilitating returned instances; can be null</param>
        /// <param name="destroyBlock">delegate for properly tearing down superfluous instances; can be null</param>
        public StrictPool(uint low, uint high, Returning<T> createBlock,
            Action<T> rehabBlock, Action<T> destroyBlock)
            : base(low, high, createBlock, rehabBlock, destroyBlock)
        {
            while(Count < low)
            {
                available.Enqueue(CreateElement());
            }
        }

        /// <summary>
        /// Return true if this pool has elements available to be obtained
        /// without blocking.
        /// </summary>
        public override bool Available
        {
            get { return assigned.Count < High; }
        }

        /// <summary>
        /// Obtain an element from the pool.
        /// The requestor is blocked if there are no elements available;
        /// this behaviour may be different in subclasses.
        /// Callers are *required* to return the element when finished via
        /// <see cref="Return"/> or <see cref="Ruined"/>.
        /// </summary>
        /// <returns>a pool element</returns>
        public override T Obtain()
        {
            lock(lockObject)
            {
                // If the queue is empty, wait for an item to be added
                // Note that this is a while loop, as we may be pulsed
                // but not actually run before another thread has come in and
                // consumed the newly added object. In such a case, we
                // need to wait again for another pulse.
                while(!Available)
                {
                    // This releases queueLock, only reacquiring it
                    // after being woken up by a call to Pulse
                    Monitor.Wait(lockObject);
                }
                T poolElement = base.Obtain();
                return poolElement;
            }
        }

        public override void Return(T o)
        {
            lock (lockObject)
            {
                base.Return (o);
                // We always need to pulse, even if the pool wasn't
                // empty before. Otherwise, if we add several items
                // in quick succession, we may only pulse once, waking
                // a single thread up, even if there are multiple
                // threads waiting for items.            
                Monitor.Pulse(lockObject);
            }
        }

        public override void Ruined(T o)
        {
            lock (lockObject)
            {
                base.Ruined(o);
                // We always need to pulse, even if the pool wasn't
                // empty before. Otherwise, if we add several items
                // in quick succession, we may only pulse once, waking
                // a single thread up, even if there are multiple
                // threads waiting for items.            
                Monitor.Pulse(lockObject);
            }
        }

    }
}

