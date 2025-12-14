namespace Azure.Functions.DistributedLock.Attributes
{
  /// <summary>
  /// Backwards-compatible attribute used during in-process function migrations.
  /// This attribute behaves identically to <see cref="DistributedLockAttribute"/>
  /// and is provided to ease an in-place upgrade from the Functions SDK's
  /// built-in <c>SingletonAttribute</c> to this distributed lock implementation.
  /// Apply this attribute to a function method to ensure it is executed with a
  /// distributed lock identified by <see cref="LockKey"/>.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="SingletonAttribute"/> class
  /// with the specified lock key.
  /// </remarks>
  /// <param name="lockKey">A string that uniquely identifies the lock resource.</param>
  [AttributeUsage(AttributeTargets.Method)]
  public sealed class SingletonAttribute(string lockKey = "") : Attribute, Interfaces.IDistributedLockAttribute
  {

    /// <summary>
    /// Gets the lock key that identifies the resource to be locked.
    /// </summary>
    public string LockKey { get; } = lockKey;
  }
}
