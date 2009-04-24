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
            else if (MissEvent != null)
            {
                MissEvent(para);
            }
        }
    }
}
