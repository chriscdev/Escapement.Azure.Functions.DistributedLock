namespace Escapement.Azure.Functions.DistributedLock.Exceptions
{
  public class LockUnavailableException : Exception
  {
    public string LockKey { get; }

    public LockUnavailableException(string lockKey)
        : base($"Could not acquire lock for {lockKey} within the configured timeout.")
    {
      LockKey = lockKey;
    }

    public LockUnavailableException(string lockKey, Exception inner)
        : base($"Could not acquire lock for {lockKey} within the configured timeout.", inner)
    {
      LockKey = lockKey;
    }
  }
}
