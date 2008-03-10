using System;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Threading;
using System.Diagnostics;

namespace GT.Net
{
    /// <summary>A high-resolution high-performance timer.</summary>
    public class HPTimer
    {
        private long startTime;
        private double freqInverse;

        /// <summary>This timer ticks this many times per second.
        /// This is a measure of how accurate this timer is on this particular machine.</summary>
        public long Frequency;

        /// <summary>A measure of how many seconds have passed
        /// between the last two calls of the method Update.</summary>
        public double ElapsedInSeconds;

        /// <summary>A measure of how many milliseconds have passed
        /// between the last two calls of the method Update.</summary>
        public double ElapsedInMilliseconds;

        /// <summary>A measure of how many timer ticks have passed in total
        /// between the the creation of this object and the last call of Update.</summary>
        public long Time;

        /// <summary>A measure of how many seconds have passed in total
        /// between the the creation of this object and the last call of Update.</summary>
        public double TimeInSeconds;

        /// <summary>A measure of how many milliseconds have passed in total
        /// between the the creation of this object and the last call of Update.</summary>
        public double TimeInMilliseconds;

        /// <summary>A high-resolution high-performance timer.</summary>
        public HPTimer()
        {
            startTime = 0;
            Time = 0;
            Frequency = 0;

            if (!Stopwatch.IsHighResolution)
            {
                Console.WriteLine("warning: System.Diagnostics.Stopwatch is not high-resolution");
            }
            Frequency = Stopwatch.Frequency;
            freqInverse = 1d / (double)Frequency;
        }

        /// <summary>Starts the timer timing.</summary>
        public void Start()
        {
            //begin on a new time slice
            Thread.Sleep(0);

            startTime = Stopwatch.GetTimestamp();
        }

        /// <summary>Updates the current values of the timer.</summary>
        public void Update()
        {
            Time = Stopwatch.GetTimestamp();

            ElapsedInSeconds = (Time - startTime) * freqInverse;
            TimeInSeconds = Time * freqInverse;

            ElapsedInMilliseconds = ElapsedInSeconds * 1000;
            TimeInMilliseconds = TimeInSeconds * 1000;

            startTime = Time;
            if (ElapsedInSeconds < 0) //start anew, we overflowed
                Update();
        }
    }
}

