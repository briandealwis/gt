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
using System.Diagnostics;

namespace GT.Utils 
{
    /// <summary>
    /// Manage a sliding window of size <see cref="WindowSize"/> within an available space
    /// of size <see cref="Capacity"/>.  Trigger the <see cref="FrameExpired"/> as the window
    /// moves and old frames leave the window.
    /// </summary>
    public class SlidingWindowManager 
    {
        /// <summary>
        /// An event triggered whenever a frame leaves the window.
        /// The window will have been advanced by the time
        /// this event is triggered.
        /// </summary>
        public event Action<uint> FrameExpired;

        protected uint nextFrame = 0;
        protected uint allocated = 0;

        /// <summary>
        /// Return the size of the space.
        /// </summary>
        public uint Capacity { get; protected set; }

        /// <summary>
        /// Return the size of the window.
        /// </summary>
        public uint WindowSize { get; protected set; }

        /// <summary>
        /// Create a sliding window of size <see cref="windowSize"/> within a space
        /// defined as twice the window's size.
        /// </summary>
        /// <param name="windowSize">the window size</param>
        public SlidingWindowManager(uint windowSize) : this(windowSize * 2, windowSize) {}

        /// <summary>
        /// Create a sliding window of size <see cref="windowSize"/> within a space
        /// of size <see cref="capacity"/>.  <see cref="capacity"/> must be less
        /// than <see cref="windowSize"/>.
        /// </summary>
        /// <param name="windowSize">the space size</param>
        /// <param name="capacity">the window size</param>
        public SlidingWindowManager(uint windowSize, uint capacity)
        {
            if(windowSize >= capacity)
            {
                throw new ArgumentException("windowSize must be less than capacity");
            }
            Capacity = capacity;
            WindowSize = windowSize;
        }

        /// <summary>
        /// Return the size of the window.
        /// </summary>
        public int Count { get { return (int)allocated; }}

        protected uint FirstOutstandingFrame 
        {
            get { 
                Debug.Assert(allocated != 0);
                return nextFrame >= allocated 
                    ? nextFrame - allocated
                    : Capacity - (allocated - nextFrame);
            }
        }

        protected uint LastOutstandingFrame 
        {
            get { 
                Debug.Assert(allocated != 0);
                return nextFrame > 0 ? nextFrame - 1 : Capacity - 1;
            }
        }

        /// <summary>
        /// Return true if the provided frame is within the current window
        /// </summary>
        /// <param name="frame">the frame number</param>
        /// <returns>true if the frame is within the current window, false otherwise</returns>
        public bool IsActive(uint frame) 
        {
            if(allocated == 0) { return false; }
            Debug.Assert(allocated <= WindowSize);
            if(nextFrame >= allocated)
            {
                return frame >= nextFrame - allocated && frame < nextFrame;
            }
            // if allocated < nextFrame then we have wrappage
            return frame < nextFrame || frame >= Capacity - (allocated - nextFrame);
        }

        /// <summary>
        /// Check to see if this is an anticipated frame -- something we haven't
        /// yet seen, but is within our expected boundary.  This is as opposed to
        /// a frame already within our current purview or an expired frame.
        /// </summary>
        /// <param name="frame">the frame seen</param>
        /// <returns>true if this is a frame within our expected boundary</returns>
        protected bool IsExpectedFrame(uint frame)
        {
            if(nextFrame + WindowSize < Capacity)
            {
                return frame >= nextFrame && frame < nextFrame + WindowSize;
            }
            return frame >= nextFrame || frame < WindowSize - (Capacity - nextFrame);
        }

        /// <summary>
        /// Indicate that the given frame has been seen; check to see
        /// if the window should advance, and trigger <see cref="FrameExpired"/>
        /// for any newly-expired frames.
        /// </summary>
        /// <param name="frame">the frame seen</param>
        /// <returns>true if the frame is within the (possibly advanced) window, false otherwise</returns>
        /// <exception cref="ArgumentOutOfRangeException">thrown if <see cref="frame"/>
        /// is out side of the range of the window</exception>
        public bool Seen(uint frame) 
        {
            if(frame >= Capacity) { throw new ArgumentOutOfRangeException("frame"); }
            Debug.Assert(allocated <= WindowSize);
            if (IsActive(frame)) { return true; }
            if (!IsExpectedFrame(frame)) { return false; }

            // If we haven't seen any frame previously (allocated == 0), then 
            // there is no old FirstOutstandingFrame, and we don't have to shift much
            uint oldFirstFrame = allocated > 0 ? FirstOutstandingFrame : uint.MaxValue;
            uint shift = allocated > 0 ? (frame + Capacity - nextFrame) % Capacity + 1 : 1;
            uint expired = shift > WindowSize - allocated ? shift - (WindowSize - allocated) : 0;

            allocated = Math.Min(WindowSize, allocated + shift);
            nextFrame = (frame + 1) % Capacity;
            if (FrameExpired != null && expired > 0) {
                while (oldFirstFrame != FirstOutstandingFrame)
                {
                    FrameExpired(oldFirstFrame);
                    oldFirstFrame = (oldFirstFrame + 1) % Capacity;
                }
            }
            return true;
        }

    }
}
