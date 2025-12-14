namespace Azure.Functions.DistributedLock.Interfaces
{
  public interface IDistributedLockHandler
  {
    /// <summary>
    /// The core method for acquiring the lock. Returns null if already held.
    /// </summary>
    /// <param name="lockKey">Unique lock identifier.</param>
    /// <param name="ct">Cancellation token to abort</param>
    /// <returns></returns>
    Task<ILockHandle?> TryAcquireLockAsync(string lockKey, CancellationToken ct = default);

    /// <summary>
    /// Retry acquiring a lock until the specified timeout elapses.
    /// </summary>
    /// <param name="lockKey">Unique lock identifier.</param>
    /// <param name="timeout">Maximum time to wait for the lock.</param>
    /// <param name="ct">Cancellation token to abort waiting earlier.</param>
    /// <returns>A lease handle when acquired; otherwise null if timeout or cancellation occurs.</returns>
    public Task<ILockHandle?> WaitForAcquireLockAsync(string lockKey, TimeSpan timeout, CancellationToken ct = default);

    /// <summary>
    /// Retry acquiring a lock until the provided <paramref name="ct"/> is canceled.
    /// The caller may supply a CancellationTokenSource with a timeout to control wait duration.
    /// </summary>
    /// <param name="lockKey">Unique lock identifier.</param>
    /// <param name="ct">Cancellation token that controls the wait lifetime.</param>
    /// <returns>A lease handle when acquired; otherwise null if the token is canceled before acquisition.</returns>
    public Task<ILockHandle?> WaitForAcquireLockAsync(string lockKey, CancellationToken ct);
  }
}
