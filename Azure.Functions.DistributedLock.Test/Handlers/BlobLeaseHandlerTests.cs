using Azure.Functions.DistributedLock.Handlers;
using Azure.Functions.DistributedLock.Test.Mocks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Moq;

namespace Azure.Functions.DistributedLock.Test.Handlers
{
  public class BlobLeaseHandlerTests
  {
    [Fact]
    public void Constructor_WithNullContainer_Throws()
    {
      Assert.Throws<ArgumentNullException>(() => new BlobLeaseHandler(containerClient: null!));
    }

    [Fact]
    public async Task WaitForAcquireLockAsync_WithAlreadyCancelledToken_ReturnsNull()
    {
      var mockContainer = new Mock<BlobContainerClient>(MockBehavior.Strict);
      var mockLease = new Mock<BlobLeaseClient>(MockBehavior.Strict);
      var locker = new TestBlobLeaseHandler(mockContainer.Object, mockLease.Object);

      using var cts = new CancellationTokenSource();
      cts.Cancel();

      var result = await locker.WaitForAcquireLockAsync("key", cts.Token);
      Assert.Null(result);
    }

    [Fact]
    public async Task AcquireLockAsync_WhenAcquireSuccessful_ReturnsHandleAndReleasesOnDispose()
    {
      var mockContainer = new Mock<BlobContainerClient>(MockBehavior.Strict);
      var mockBlob = new Mock<BlobClient>(MockBehavior.Strict);
      var mockLease = new Mock<BlobLeaseClient>(MockBehavior.Strict);

      mockContainer.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(mockBlob.Object);
      mockBlob.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue(false, null!));
      mockBlob.Setup(b => b.UploadAsync(It.IsAny<BinaryData>(), true, It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue((BlobContentInfo)null!, null!));

      // Use TestableBlobStorageLock to inject the mocked lease client

      // Simulate successful acquire
      mockLease.Setup(l => l.AcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue(Mock.Of<BlobLease>(bl => bl.LeaseId == "lease"), null!));
      // Renewal and release should be callable
      mockLease.Setup(l => l.RenewAsync(It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue((BlobLease)null!, null!)).Verifiable();
      mockLease.Setup(l => l.ReleaseAsync(It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue((ReleasedObjectInfo)null!, null!)).Verifiable();

      var locker = new TestBlobLeaseHandler(mockContainer.Object, mockLease.Object);

      var handle = await locker.TryAcquireLockAsync("key");
      Assert.NotNull(handle);

      await handle!.DisposeAsync();

      mockLease.Verify(l => l.ReleaseAsync(It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task AcquireLockAsync_WhenAcquireFails_ReturnsNull()
    {
      var mockContainer = new Mock<BlobContainerClient>(MockBehavior.Strict);
      var mockBlob = new Mock<BlobClient>(MockBehavior.Strict);
      var mockLease = new Mock<BlobLeaseClient>(MockBehavior.Strict);

      mockContainer.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(mockBlob.Object);
      mockBlob.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue(false, null!));
      mockBlob.Setup(b => b.UploadAsync(It.IsAny<BinaryData>(), true, It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue((BlobContentInfo)null!, null!));

      // Simulate failure to acquire
      mockLease.Setup(l => l.AcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RequestFailedException("Conflict"));

      var locker = new TestBlobLeaseHandler(mockContainer.Object, mockLease.Object);

      var handle = await locker.TryAcquireLockAsync("key");
      Assert.Null(handle);
    }

    [Fact]
    public async Task WaitForAcquireLockAsync_TimesOut_ReturnsNull()
    {
      var mockContainer = new Mock<BlobContainerClient>(MockBehavior.Strict);
      var mockBlob = new Mock<BlobClient>(MockBehavior.Strict);
      var mockLease = new Mock<BlobLeaseClient>(MockBehavior.Strict);

      mockContainer.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(mockBlob.Object);
      mockBlob.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue(false, null!));
      mockBlob.Setup(b => b.UploadAsync(It.IsAny<BinaryData>(), true, It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue((BlobContentInfo)null!, null!));

      // Always fail acquire
      mockLease.Setup(l => l.AcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RequestFailedException("Conflict"));

      var locker = new TestBlobLeaseHandler(mockContainer.Object, mockLease.Object);

      var handle = await locker.WaitForAcquireLockAsync("key", TimeSpan.FromSeconds(1));
      Assert.Null(handle);
    }
    [Fact]
    public async Task TryAcquireLockAsync_LockAvailable_ReturnsHandle()
    {
      // Arrange
      var mockLeaseClient = new MockBlobLeaseClient();
      var mockContainerClient = new Mock<BlobContainerClient>();
      var handler = new TestBlobLeaseHandler(mockContainerClient.Object, mockLeaseClient);

      // Act
      var handle = await handler.TryAcquireLockAsync("test-key");

      // Assert
      Assert.NotNull(handle);
      // Clean up
      if (handle != null) await handle.DisposeAsync();
    }

    [Fact]
    public async Task TryAcquireLockAsync_LockUnavailable_ReturnsNull()
    {
      // Arrange
      // Use the concrete MockBlobLeaseClient that can be configured to throw on acquire
      var mockLeaseClient = new MockBlobLeaseClient(onRenew: null, onRelease: null, onAcquireException: new RequestFailedException(409, "Lock held"));

      var mockContainerClient = new Mock<BlobContainerClient>();
      var handler = new TestBlobLeaseHandler(mockContainerClient.Object, mockLeaseClient);

      // Act
      var handle = await handler.TryAcquireLockAsync("test-key");

      // Assert
      Assert.Null(handle);
    }


    [Fact]
    public async Task BlobLeaseHandle_RenewsInTheBackground()
    {
      // Arrange
      int renewalCount = 0;

      // The mock client tracks the renewal count
      var mockLeaseClient = new MockBlobLeaseClient(onRenew: () => { renewalCount++; });
      var mockContainerClient = new Mock<BlobContainerClient>();
      var handler = new TestBlobLeaseHandler(mockContainerClient.Object, mockLeaseClient);

      // Act: Acquire the lock, which creates and starts the renewal handle
      var handle = await handler.TryAcquireLockAsync("renewal-test", TimeSpan.FromSeconds(5));

      Assert.NotNull(handle);

      // Assert: Wait for 10 seconds (renewal interval is 5s)
      // This should allow renewal to fire at least once.
      await Task.Delay(TimeSpan.FromSeconds(10));

      // Assert: Renewal occurred
      Assert.True(renewalCount >= 1, $"Expected at least 1 renewal, found {renewalCount}");

      // Cleanup: Dispose the handle
      await handle!.DisposeAsync();
    }

    [Fact]
    public async Task BlobLeaseHandle_StopsRenewalAndReleasesLockOnDispose()
    {
      // Arrange
      int renewalCount = 0;
      bool released = false;


      var mockLeaseClient = new MockBlobLeaseClient(() => { renewalCount++; }, () => { released = true; });
      var mockContainerClient = new Mock<BlobContainerClient>();
      var handler = new TestBlobLeaseHandler(mockContainerClient.Object, mockLeaseClient);

      // Act: Acquire the lock and let it run for a moment
      var handle = await handler.TryAcquireLockAsync("disposal-test");
      Assert.NotNull(handle);

      // Wait just 5 seconds (not long enough for a scheduled renewal)
      await Task.Delay(TimeSpan.FromSeconds(5));

      // Assert: Renewal count should be 0 before disposal
      Assert.Equal(0, renewalCount);
      Assert.False(released);

      // Act: Dispose the handle
      await handle!.DisposeAsync();

      // Assert: The lock should have been released
      Assert.True(released);

      // Assert: Wait a moment to ensure no stray renewals fire after disposal
      await Task.Delay(TimeSpan.FromSeconds(5));

      // Assert: Renewal count should still be 0 (or very close, if race condition)
      Assert.Equal(0, renewalCount);
    }
  }
}
