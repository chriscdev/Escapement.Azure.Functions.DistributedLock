namespace Escapement.Azure.Functions.DistributedLock.Interfaces
{
  public interface ILockHandle : IAsyncDisposable
  {
    // Marker interface for the acquired lock object
    // IAsyncDisposable handles renewal cancellation and release
  }
}
