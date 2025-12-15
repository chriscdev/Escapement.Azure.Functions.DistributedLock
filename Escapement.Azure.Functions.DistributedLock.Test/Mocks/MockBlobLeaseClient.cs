using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Moq;

namespace Escapement.Azure.Functions.DistributedLock.Test.Mocks
{
  public class MockBlobLeaseClient(Action? onRenew = null, Action? onRelease = null, Exception? onAcquireException = null) : BlobLeaseClient
  {
    private readonly Action? _onRenew = onRenew;
    private readonly Action? _onRelease = onRelease;
    private readonly Exception? _onAcquireException = onAcquireException;

    public override Task<Response<BlobLease>> AcquireAsync(TimeSpan duration, RequestConditions requestConditions, CancellationToken cancellationToken = default)
    {
      if (_onAcquireException != null)
      {
        return Task.FromException<Response<BlobLease>>(_onAcquireException);
      }

      // Simulate successful acquisition
      return Task.FromResult(new Mock<Response<BlobLease>>().Object);
    }

    public override Task<Response<BlobLease>> RenewAsync(RequestConditions requestConditions, CancellationToken cancellationToken = default)
    {
      _onRenew?.Invoke(); // Track renewal calls
      return Task.FromResult(new Mock<Response<BlobLease>>().Object);
    }

    public override Task<Response<ReleasedObjectInfo>> ReleaseAsync(RequestConditions requestConditions = default, CancellationToken cancellationToken = default)
    {
      _onRelease?.Invoke(); // Track release calls
      return Task.FromResult(new Mock<Response<ReleasedObjectInfo>>().Object);
    }
  }
}
