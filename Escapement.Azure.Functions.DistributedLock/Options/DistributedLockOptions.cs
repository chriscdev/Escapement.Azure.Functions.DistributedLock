using System;

namespace Escapement.Azure.Functions.DistributedLock.Options
{
    public class DistributedLockOptions
    {
        /// <summary>
        /// Default timeout for waiting to acquire a distributed lock.
        /// </summary>
        public TimeSpan WaitTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}
