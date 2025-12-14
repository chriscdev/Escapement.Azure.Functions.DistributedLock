using Azure.Functions.DistributedLock.Interfaces;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace Azure.Functions.DistributedLock.Handlers
{
  public class BlobLeaseHandler : IDistributedLockHandler
  {
    private readonly BlobContainerClient _containerClient;
    private static readonly TimeSpan DefaultLeaseRenewalInterval = TimeSpan.FromSeconds(40);

    public BlobLeaseHandler(BlobServiceClient client)
    {
      _containerClient = client.GetBlobContainerClient("locks");
      _containerClient.CreateIfNotExists();
    }

    public BlobLeaseHandler(BlobContainerClient containerClient)
    {
      _containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
    }

    protected virtual BlobLeaseClient CreateLeaseClient(BlobClient blob) => blob.GetBlobLeaseClient();

    public Task<ILockHandle?> TryAcquireLockAsync(string lockKey, CancellationToken ct = default)
    {
      return TryAcquireLockAsync(lockKey, DefaultLeaseRenewalInterval, ct);
    }

    public async Task<ILockHandle?> TryAcquireLockAsync(string lockKey, TimeSpan renewalInterval, CancellationToken ct = default)
    {
      var blob = _containerClient.GetBlobClient(lockKey);

      if (!await blob.ExistsAsync(ct))
      {
        try { await blob.UploadAsync(BinaryData.Empty, overwrite: true, ct); }
        catch (RequestFailedException) { }
      }

      var leaseClient = CreateLeaseClient(blob);

      try
      {
        await leaseClient.AcquireAsync(TimeSpan.FromSeconds(60), cancellationToken: ct);
      }
      catch (RequestFailedException)
      {
        return null; // Lock already held
      }
      catch (OperationCanceledException) when (ct.IsCancellationRequested)
      {
        // If cancellation happened during acquire, don't return a handle
        return null;
      }

      return new BlobLeaseHandle(leaseClient, renewalInterval, ct);
    }

    public async Task<ILockHandle?> WaitForAcquireLockAsync(string lockKey, TimeSpan timeout, CancellationToken ct = default)
    {
      using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
      linked.CancelAfter(timeout);
      var token = linked.Token;

      var delay = TimeSpan.FromSeconds(1);
      while (!token.IsCancellationRequested)
      {
        // Use the linked token for acquisition attempts
        var handle = await TryAcquireLockAsync(lockKey, token);
        if (handle != null) return handle;

        try
        {
          await Task.Delay(delay, token);
          delay = TimeSpan.FromSeconds(Math.Min(5, delay.TotalSeconds * 2));
        }
        catch (TaskCanceledException)
        {
          break;
        }
      }

      return null;
    }

    public async Task<ILockHandle?> WaitForAcquireLockAsync(string lockKey, CancellationToken ct)
    {
      if (ct.IsCancellationRequested) return null;
      return await WaitForAcquireLockAsync(lockKey, TimeSpan.MaxValue, ct);
    }

    /// <summary>
    /// This internal class manages the active lease lifecycle (renewal and release).
    /// </summary>
    private class BlobLeaseHandle : ILockHandle
    {
      private readonly BlobLeaseClient _client;
      private readonly Task _renewalTask;
      private readonly CancellationTokenSource _linkedCts;

      /// <summary>
      /// ctor
      /// </summary>
      /// <param name="client"></param>
      /// <param name="renewalInterval"></param>
      /// <param name="externalCt"></param>
      /// <exception cref="ArgumentOutOfRangeException"></exception>
      public BlobLeaseHandle(BlobLeaseClient client, TimeSpan renewalInterval, CancellationToken externalCt)
      {
        // Validate the interval is less than the 60s max lease time
        if (renewalInterval >= TimeSpan.FromSeconds(60))
        {
          throw new ArgumentOutOfRangeException(nameof(renewalInterval), "Renewal interval must be less than 60 seconds.");
        }

        _client = client;

        // Link external token (caller cancellation) and internal token (DisposeAsync call)
        CancellationTokenSource internalCts = new();
        _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(internalCts.Token, externalCt);

        // 1. Start Auto-Renewal task
        _renewalTask = Task.Run(async () =>
        {
          while (!_linkedCts.Token.IsCancellationRequested)
          {
            try
            {
              await Task.Delay(renewalInterval, _linkedCts.Token);
              await _client.RenewAsync();
            }
            catch (TaskCanceledException)
            {
              break; // Exit gracefully
            }
            catch (Exception)
            {
              // Handle renewal failure
            }
          }
        }, _linkedCts.Token);
      }

      public async ValueTask DisposeAsync()
      {
        // 2. Cancellation: Signal cancellation
        _linkedCts.Cancel();

        // Wait for renewal task to exit
        try { await _renewalTask; } catch { }

        // 3. Release the lock
        try { await _client.ReleaseAsync(); }
        catch { }

        _linkedCts.Dispose();
      }
    }
  }
}
