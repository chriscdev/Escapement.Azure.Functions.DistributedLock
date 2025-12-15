namespace Escapement.Azure.Functions.DistributedLock.Interfaces
{
    /// <summary>
    /// Common contract for distributed lock attributes exposing the lock key.
    /// </summary>
    public interface IDistributedLockAttribute
    {
        /// <summary>
        /// Gets the lock key that identifies the resource to be locked.
        /// </summary>
        string LockKey { get; }
    }
}
