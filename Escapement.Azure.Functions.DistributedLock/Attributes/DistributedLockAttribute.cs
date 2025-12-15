namespace Escapement.Azure.Functions.DistributedLock.Attributes
{
  /// <summary>
  /// Common contract for distributed lock attributes exposing the lock key.
  /// </summary>
  /// <summary>
  /// An attribute that marks a function method to be executed with a distributed lock.
  /// The <see cref="LockKey"/> identifies the resource to be locked. When applied,
  /// middleware will attempt to acquire the lock before invoking the method and
  /// release it afterwards.
  /// </summary>
  [AttributeUsage(AttributeTargets.Method)]
  public sealed class DistributedLockAttribute : Attribute, Interfaces.IDistributedLockAttribute
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedLockAttribute"/> class
    /// with the specified lock key.
    /// </summary>
    /// <param name="lockKey">A string that uniquely identifies the lock resource.</param>
    public DistributedLockAttribute(string lockKey)
    {
      LockKey = lockKey;
    }

    /// <summary>
    /// Gets the lock key that identifies the resource to be locked.
    /// </summary>
    public string LockKey { get; }
  }
}
