# Escapement.Azure.Functions.DistributedLock

A small library to provide distributed locking for Azure Functions (Isolated Worker) using Azure Blob Storage leases. It is a lightweight replacement for `[Microsoft.Azure.WebJobs.Singleton]` that supports declarative attributes and programmatic lock acquisition.

Package: `Escapement.Azure.Functions.DistributedLock`

Summary
- Declarative locking via `[DistributedLock("key-{id}")]` attribute.
- Programmatic locking via `IDistributedLockHandlerFactory` and `IDistributedLockHandler`.
- Uses Azure Blob leases with automatic background renewal and safe release.
- Testable design suitable for unit tests.

Install

```bash
dotnet add package Azure.Functions.DistributedLock
```

Quick start

1) Register services in `Program.cs`:

```csharp
services.AddSingleton(sp => new Azure.Storage.Blobs.BlobServiceClient(
    Environment.GetEnvironmentVariable("AzureWebJobsStorage")
));
services.AddSingleton<IDistributedLockHandlerFactory, BlobLeaseHandlerFactory>();
// If using middleware: workerApplication.UseMiddleware<DistributedLockMiddleware>();
```

2) Declarative usage (attribute on function):

```csharp
[Function("ProcessOrder")]
[DistributedLock("order-lock-{orderId}")]
public async Task Run(string orderId, ILogger log)
{
    // If this runs, the lock was acquired and is being renewed.
}
```

3) Programmatic usage (fine-grained control):

```csharp
var handler = _lockHandlerFactory.CreateHandler();
await using var handle = await handler.TryAcquireLockAsync("my-key");
if (handle == null) { /* locked: skip or retry */ }
// critical section
```

Recommendations
- Use a consistent storage account and container name across hosts.
- Prefer `WaitForAcquireLockAsync` with timeout for background jobs that must complete.

License and contribution
See repository for full docs, tests, and contribution guidelines.