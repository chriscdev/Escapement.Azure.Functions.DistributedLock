# Escapement.Azure.Functions.DistributedLock

A small library to provide distributed locking for Azure Functions (Isolated Worker) using Azure Blob Storage leases. It is a lightweight replacement for `[Microsoft.Azure.WebJobs.Singleton]` that supports declarative attributes and programmatic lock acquisition.

Package: `Escapement.Azure.Functions.DistributedLock`

## Summary
- Declarative locking via `[Singleton("key-{id}")]` attribute for in-place migration from in-process to isolated Functions worker model.
- Declarative locking via `[DistributedLock("key-{id}")]` attribute.
- Programmatic locking via `IDistributedLockHandlerFactory` and `IDistributedLockHandler`.
- Uses Azure Blob leases with automatic background renewal and safe release.
- Testable design suitable for unit tests.

## Install

```bash
dotnet add package Escapement.Azure.Functions.DistributedLock
```

## Quick start

### 1) Register services in `Program.cs`:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Azure.Storage.Blobs;
using Escapement.Azure.Functions.DistributedLock.Middleware; //exclude if not using the middleware
using Escapement.Azure.Functions.DistributedLock;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(workerApplication =>
    {
        // Register middleware if using the [Singleton] or [DistributedLock] attributes
        workerApplication.UseMiddleware<DistributedLockMiddleware>();
    })
    .ConfigureServices(services =>
    {
        // Register the default distributed lock handler (used by both Declarative usage and Programmatic usage scenarios)
        // Uses the AzureWebJobsStorage environment varaible by default for the Blob storage connection string
        services.AddBlobServiceDistributedLock();
    })
    .Build();

await host.RunAsync();

```

### 2) Declarative usage (attribute on function):

Make sure to add the `DistributedLockMiddleware` middleware to `Program.cs`
```csharp
[Function("ProcessOrder")]
[Singleton("order-lock-{orderId}")]
public async Task Run(string orderId, ILogger log)
{
    // If this runs, the lock was acquired and is being renewed.
}
```

or

```csharp
[Function("ProcessOrder")]
[DistributedLock("order-lock-{orderId}")]
public async Task Run(string orderId, ILogger log)
{
    // If this runs, the lock was acquired and is being renewed.
}
```

### 3) Programmatic usage (fine-grained control):

Does not use the middleware.

```csharp
var handler = _lockHandlerFactory.CreateHandler();
await using var handle = await handler.TryAcquireLockAsync("my-key");
if (handle == null) { /* locked: skip or retry */ }
// critical section
```

## Attribute examples

| Attribute usage | Lock key | Behavior |
|---|---:|---|
| `[Singleton]` or `[DistributedLock]` | `Function name` | Only one instance runs per function accross all hosts (default). |
| `[Singleton("GlobalProcess")]` or `[DistributedLock("GlobalProcess")]` | `GlobalProcess` | Only one instance runs across all hosts. |
| `[Singleton("User-{userId}")]` or `[DistributedLock("User-{userId}")]` | `User-1234` | Only one instance runs for a specific user ID. |

## Recommendations
- Use a consistent storage account and container name across hosts.
- Prefer `WaitForAcquireLockAsync` with timeout for background jobs that must complete.

## License and contribution
See repository for full docs, tests, and contribution guidelines.