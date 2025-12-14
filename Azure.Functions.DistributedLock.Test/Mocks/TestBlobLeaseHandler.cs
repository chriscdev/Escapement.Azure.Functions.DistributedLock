using Azure.Functions.DistributedLock.Handlers;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Moq;

namespace Azure.Functions.DistributedLock.Test.Mocks
{
  // Testable Handler that overrides the factory method
  public class TestBlobLeaseHandler : BlobLeaseHandler
  {
    // Mock BlobClient is needed for ExistsAsync calls
    public Mock<BlobClient> MockBlobClient { get; } = new Mock<BlobClient>();

    // The specific lease client to be returned by the factory hook
    private readonly BlobLeaseClient _leaseClientToReturn;
    public TestBlobLeaseHandler(BlobContainerClient containerClient, BlobLeaseClient leaseClientToReturn)
        : base(containerClient)
    {
      _leaseClientToReturn = leaseClientToReturn;

      // Setup mock blob client existence check
      MockBlobClient
          .Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>()))
          .ReturnsAsync(Response.FromValue(true, null!));

      // Setup GetBlobClient call on the passed container client (expects the caller passed a mock.Object)
      try
      {
        var mockContainer = Mock.Get(containerClient);
        mockContainer
            .Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(MockBlobClient.Object);
      }
      catch
      {
        // If the provided containerClient is not a mock proxy, we cannot setup it here.
        // Tests that pass a non-mock container can still rely on overriding CreateLeaseClient.
      }
    }
    protected override BlobLeaseClient CreateLeaseClient(BlobClient blob)
    {
      // Return the specific mock we set up
      return _leaseClientToReturn;
    }
  }
}
