using System;

namespace GT
{
    /// <summary>
    /// Defines methods for starting, stopping, and disposing of an instance.
    /// A stopped instance may be started again.  A disposed instance can never
    /// be restarted.  A stopped instance can be stopped multiple times.
    /// To properly support IDisposable, an instance can be disposed whether it
    /// is started or stopped.
    /// </summary>
    public interface IStartable : IDisposable
    {
        /// <summary>
        /// Start the instance.  Starting an instance may throw an exception on error.
        /// </summary>
        void Start();

        /// <summary>
        /// Stop the instance.  Instances can be stopped multiple times.
        /// Stopping an instance may throw an exception on error.
        /// </summary>
        void Stop();

        /// <summary>
        /// Return true if the instance is currently active.
        /// </summary>
        bool Active { get; }
    }
}
