namespace Azure.Functions.DistributedLock.Interfaces
{
  public interface IDistributedLockHandlerFactory
  {
    // Factory method allows the middleware to get a new handler instance per invocation
    IDistributedLockHandler CreateHandler(string lockKey);
  }
}
