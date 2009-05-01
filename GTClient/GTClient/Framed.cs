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
using System.Collections.Generic;
using System.Text;

namespace GT.Net
{
    /// <summary>Used to set a certain event to only occur every so often.  (Thread-safe)</summary>
    public class Frame
    {
        /// <summary>This method will handle a hit or a miss.</summary>
        public delegate void FrameDelegate(object[] para);

        /// <summary>Triggered on a hit</summary>
        public event FrameDelegate HitEvent;

        /// <summary>Triggered on a miss</summary>
        public event FrameDelegate MissEvent;

        private double lastEvent;
        private HPTimer timer;

        /// <summary>A hit will only occur once during this interval, otherwise it will miss.</summary>
        public double Interval;

        /// <summary></summary>
        public Frame(double interval)
        {
            timer = new HPTimer();
            this.Interval = interval;
        }

        /// <summary>Throw either a hit or a miss.</summary>
        public void SlipTrigger(object[] para)
        {
            timer.Update();
            int currentTime = (int)timer.TimeInMilliseconds;

            if (lastEvent > currentTime)
                lastEvent = 0;

            if (Interval + lastEvent <= currentTime)
            {
                if(HitEvent != null)
                {
                    lastEvent = currentTime;
                    HitEvent(para);
                }
            }
            else
            {
                if (MissEvent != null)
                    MissEvent(para);
            }
        }
    }
}
