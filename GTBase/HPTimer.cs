using System.Threading;
using System.Diagnostics;
using Common.Logging;

namespace GT
{
    /// <summary>A high-resolution high-performance timer.  The time is stable, only updated
    /// when explicitly commanded.
    /// </summary>
    public class HPTimer : IStartable
    {
        private Stopwatch stopwatch;
        protected long frequency;
        private long totalTicks;
        private long totalMilliseconds;
        private long lastTicks;
        private long lastMilliseconds;

        /// <summary>This timer ticks this many times per second.
        /// This is a measure of how accurate this timer is on this particular machine.</summary>
        public long Frequency { get { return frequency; } }

        /// <summary>A measure of how many seconds have passed
        /// between the last two calls of the method Update.</summary>
        public long ElapsedInSeconds { get { return ElapsedInMilliseconds / 1000; } }

        /// <summary>A measure of how many milliseconds have passed
        /// between the last two calls of the method Update.</summary>
        public long ElapsedInMilliseconds { get { return totalMilliseconds - lastMilliseconds; } }

        /// <summary>A measure of how many timer ticks have passed in total
        /// between the the creation of this object and the last call of Update.</summary>
        public long Ticks { get { return totalTicks; } }

        /// <summary>A measure of how many seconds have passed in total
        /// between the the creation of this object and the last call of Update.</summary>
        public long TimeInSeconds { get { return totalMilliseconds / 1000; } }

        /// <summary>A measure of how many milliseconds have passed in total
        /// between the the creation of this object and the last call of Update.</summary>
        /// <remarks>A long should be able to represent 584942417 years worth of milliseconds.</remarks>
        public long TimeInMilliseconds { get { return totalMilliseconds; } }

        /// <summary>A high-resolution high-performance timer.</summary>
        public HPTimer()
        {
            if (!Stopwatch.IsHighResolution)
            {
                LogManager.GetLogger(GetType()).Warn("System.Diagnostics.Stopwatch is not high-resolution");
            }
            frequency = Stopwatch.Frequency;
        }

        public bool Active { get { return stopwatch != null; } }

        /// <summary>Starts the timer timing.</summary>
        public void Start()
        {
            //begin on a new time slice
            Thread.Sleep(0);
            stopwatch = new Stopwatch();
            stopwatch.Start();
            Update();
        }

        public void Stop()
        {
            if (stopwatch != null) { stopwatch.Stop(); }
        }

        public void Dispose()
        {
            Stop();
            stopwatch = null;
        }

        /// <summary>Updates the current values of the timer.</summary>
        public void Update()
        {
            lastTicks = totalTicks;
            lastMilliseconds = totalMilliseconds;
            totalTicks = stopwatch.ElapsedTicks;
            totalMilliseconds = stopwatch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Create a new, started instance.
        /// </summary>
        /// <returns>a new, started instance</returns>
        public static HPTimer StartNew()
        {
            HPTimer t = new HPTimer();
            t.Start();
            return t;
        }
    }
}

