using Azure.Functions.DistributedLock.Interfaces;
using Azure.Storage.Blobs;

namespace Azure.Functions.DistributedLock.Handlers
{
  public class BlobLeaseHandlerFactory(BlobServiceClient blobServiceClient) : IDistributedLockHandlerFactory
  {
    private readonly BlobServiceClient _blobServiceClient = blobServiceClient;
    private const string LockContainerName = "function-locks";

    public IDistributedLockHandler CreateHandler(string lockKey)
    {
      var containerClient = _blobServiceClient.GetBlobContainerClient(LockContainerName);
      return new BlobLeaseHandler(containerClient);
    }
  }
}
