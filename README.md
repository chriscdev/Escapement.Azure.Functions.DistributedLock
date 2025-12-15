# Escapement.Azure.Functions.DistributedLock (Isolated Worker Model)

`Escapement.Azure.Functions.DistributedLock` provides a robust, testable, and customizable replacement for the
`[Microsoft.Azure.WebJobs.Singleton]` attribute, enabling distributed locking
using Azure Blob Storage leases in the Azure Functions Isolated Worker process.
This is useful when migrating functions that require concurrency control from
the in-process model to the isolated worker model.

## Features

- In place migration of the [Singleton] attribute to the isolation mode.
- Custom attribute: use `[DistributedLock]` for declarative locking.
- Dynamic keying: supports interpolation (for example, `[DistributedLock("Order-{Id}")]`).
- Safe lease management: acquires the lease, manages background renewal, and
  releases via `IAsyncDisposable`.
- Testable architecture: built around `IDistributedLockHandler` and a factory for
  easy mocking.

---

## 1. Installation and setup

### A. Dependencies

You will typically need the Azure SDK and Functions worker packages:

```bash
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.Abstractions
dotnet add package Azure.Storage.Blobs
dotnet add package Microsoft.Extensions.DependencyInjection
```

### B. Service registration (`Program.cs`)

Register the Azure SDK client and the lock handler factory in `Program.cs`.

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Azure.Storage.Blobs;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(workerApplication =>
    {
        // Register middleware if you provide it
        workerApplication.UseMiddleware<DistributedLockMiddleware>();
    })
    .ConfigureServices(services =>
    {
        // Assumes "AzureWebJobsStorage" env var is set
        services.AddSingleton(sp => new BlobServiceClient(
            Environment.GetEnvironmentVariable("AzureWebJobsStorage")
        ));

        // Register handler factory that creates IDistributedLockHandler instances
        services.AddSingleton<IDistributedLockHandlerFactory, BlobLeaseHandlerFactory>();
    })
    .Build();

host.Run();
```

---

## 2. Scenario 1 — Declarative locking (in-place migration)

Use the custom attribute and middleware as a drop-in replacement for the old
`[Singleton]` behavior. The middleware runs before the function. If the lock
cannot be acquired, execution is skipped (non-blocking).

Attribute examples

| Attribute usage | Lock key | Behavior |
|---|---:|---|
| `[Singleton("GlobalProcess")]` or `[DistributedLock("GlobalProcess")]` | `GlobalProcess` | Only one instance runs across all hosts. |
| `[Singleton("User-{userId}")]` or `[DistributedLock("User-{userId}")]` | `User-1234` | Only one instance runs for a specific user ID. |

Example function usage:

```csharp
[Function("ProcessOrder")]
[Singleton("order-lock-{orderId}")]
public async Task Run(
    [ServiceBusTrigger("orders", Connection = "ServiceBusConn")] string message,
    string orderId,
    ILogger log)
{
    log.LogInformation($"Acquired lock for Order ID: {orderId}. Processing...");

    await Task.Delay(TimeSpan.FromMinutes(1));

    // Lock is released automatically when the function exits.
}
```

or 

```csharp
[Function("ProcessOrder")]
[DistributedLock("order-lock-{orderId}")]
public async Task Run(
    [ServiceBusTrigger("orders", Connection = "ServiceBusConn")] string message,
    string orderId,
    ILogger log)
{
    log.LogInformation($"Acquired lock for Order ID: {orderId}. Processing...");

    await Task.Delay(TimeSpan.FromMinutes(1));

    // Lock is released automatically when the function exits.
}
```

---

## 3. Scenario 2 — Direct code acquisition (fine-grained control)

If you need a lock for a specific block of code or want to wait explicitly for a
lock, create a handler from the factory and use `await using` to ensure release.

### Inject the handler factory

```csharp
public class MyLockingService
{
    private readonly IDistributedLockHandlerFactory _lockHandlerFactory;
    private readonly ILogger<MyLockingService> _logger;

    public MyLockingService(IDistributedLockHandlerFactory lockHandlerFactory, ILogger<MyLockingService> logger)
    {
        _lockHandlerFactory = lockHandlerFactory;
        _logger = logger;
    }
}
```

### Lease acquisition methods

`IDistributedLockHandler` exposes the following methods:

- `TryAcquireLockAsync(string lockKey, CancellationToken ct = default)` — non-blocking; returns
  `null` immediately if the lock is held.
- `WaitForAcquireLockAsync(string lockKey, TimeSpan timeout, CancellationToken ct = default)` — retries until
  timeout is reached; returns `null` on timeout.
- `WaitForAcquireLockAsync(string lockKey, CancellationToken ct)` — retries until the token is canceled.

### Usage examples

Try acquire immediately (non-blocking):

```csharp
public async Task ProcessData(string key)
{
    var handler = _lockHandlerFactory.CreateHandler();

    // Attempt to get the lock immediately.
    var handle = await handler.TryAcquireLockAsync(key);
    if (handle == null)
    {
        _logger.LogWarning($"Lock '{key}' held. Skipping processing.");
        return;
    }

    // Lock acquired. Automatic renewal is active.
    await using (handle)
    {
        _logger.LogInformation("Lock acquired. Processing...");
        await Task.Delay(5000);
    }
}
```

Wait for lock with timeout:

```csharp
public async Task RunMigrationScript(TimeSpan maxWait)
{
    const string lockKey = "GlobalMigrationLock";
    var handler = _lockHandlerFactory.CreateHandler();

    _logger.LogInformation($"Waiting up to {maxWait.TotalSeconds} seconds for lock...");

    var handle = await handler.WaitForAcquireLockAsync(lockKey, maxWait);
    if (handle == null)
    {
        _logger.LogError("Timeout reached. Could not acquire lock. Aborting.");
        return;
    }

    await using (handle)
    {
        _logger.LogInformation("Lock acquired. Running critical migration.");
        // ... Critical logic ...
    }
}
```

---

## Notes and recommendations

- Use a stable container name (for example `function-locks`) and the same storage
  account across all hosts that must coordinate.
- Normalize lock keys to avoid exceeding storage or name length limits.
- Prefer `WaitForAcquireLockAsync` with a timeout for background or migration tasks
  to avoid blocking indefinitely.

## Contributing

PRs welcome. Follow .NET coding conventions and include tests for behavioral changes.

##🚀 Features* **Custom Attribute:** Use `[Singleton]` or `[DistributedLock]` for clean, declarative locking on function methods.
* **Dynamic Keying:** Supports dynamic key interpolation (e.g., `[DistributedLock("Order-{Id}")]`) based on function input parameters.
* **Safe Lease Management:** Automatically acquires the lease, manages **background renewal** to prevent lease expiration during long-running functions, and guarantees release via `IAsyncDisposable`.
* **Testable Architecture:** Built around `IDistributedLockHandler` and a virtual factory hook for easy mocking.

---

### 1. Installation and Setup
#### A. DependenciesYou will need the following packages:

```bash
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.Abstractions
dotnet add package Azure.Storage.Blobs
dotnet add package Microsoft.Extensions.DependencyInjection

```

#### B. Service Registration (`Program.cs`)Register the necessary Azure SDK client and the custom lock handler factory in your `Program.cs`.

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Azure.Storage.Blobs;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(workerApplication =>
    {
        // 1. Register the Middleware in the pipeline
        workerApplication.UseMiddleware<DistributedLockMiddleware>();
    })
    .ConfigureServices(services =>
    {
        // 2. Register the Azure SDK BlobServiceClient
        // Assumes "AzureWebJobsStorage" environment variable is set
        services.AddSingleton(sp => new BlobServiceClient(
            Environment.GetEnvironmentVariable("AzureWebJobsStorage")
        ));
        
        // 3. Register the Handler Factory (which provides the locking logic)
        services.AddSingleton<IDistributedLockHandlerFactory, BlobLeaseHandlerFactory>();
    })
    .Build();

host.Run();

```

---

### 2. Scenario 1: Declarative Locking (In-Place Migration)
This method uses the custom attribute and is the direct replacement for the old `[Singleton]` attribute, making it ideal for migration.

#### A. Implement the Attribute and Middleware
*(You must include the `DistributedLockAttribute`, `FunctionContextExtensions`, and `DistributedLockMiddleware` classes in your project.)*

#### B. Usage in a Function
The middleware runs **before** your function's core logic. If the lock cannot be acquired, execution is skipped (non-blocking).

| Attribute Usage | Lock Key | Behavior |
| --- | --- | --- |
| `[Singleton("GlobalProcess")]` or `[DistributedLock("GlobalProcess")]` | `GlobalProcess` | Only one instance of this function runs across all hosts. |
| `[Singleton("User-{userId}")]` or `[DistributedLock("User-{userId}")]` | `User-1234` | Only one instance runs for a specific user ID (inferred from input). |

```csharp
[Function("ProcessOrder")]
[Singleton("order-lock-{orderId}")] // Key interpolation using input parameter
public async Task Run([ServiceBusTrigger("orders", Connection = "ServiceBusConn")] 
                      string message, // Message content (if JSON)
                      string orderId, // Assumed to be bound from the message or metadata
                      ILogger log)
{
    // The middleware guarantees that if we reach this point, 
    // the lease for "order-lock-XYZ" is held and actively renewing.
    
    log.LogInformation($"Acquired lock for Order ID: {orderId}. Processing critical section...");

    await Task.Delay(TimeSpan.FromMinutes(1)); 

    // Lock is automatically released when the function exits.
}

```

or

```csharp
[Function("ProcessOrder")]
[DistributedLock("order-lock-{orderId}")] // Key interpolation using input parameter
public async Task Run([ServiceBusTrigger("orders", Connection = "ServiceBusConn")] 
                      string message, // Message content (if JSON)
                      string orderId, // Assumed to be bound from the message or metadata
                      ILogger log)
{
    // The middleware guarantees that if we reach this point, 
    // the lease for "order-lock-XYZ" is held and actively renewing.
    
    log.LogInformation($"Acquired lock for Order ID: {orderId}. Processing critical section...");

    await Task.Delay(TimeSpan.FromMinutes(1)); 

    // Lock is automatically released when the function exits.
}

```

---

### 3. Scenario 2: Direct Code Acquisition (Fine-Grained Control)
If you need a lock only around a specific block of code, or if you need to explicitly **wait** for the lock, you inject the handler factory and manage the lock directly using `await using`.

#### A. Inject the Handler
```csharp
public class MyLockingService
{
    private readonly IDistributedLockHandlerFactory _lockHandlerFactory;
    private readonly ILogger<MyLockingService> _logger;

    public MyLockingService(IDistributedLockHandlerFactory lockHandlerFactory, ILogger<MyLockingService> logger)
    {
        _lockHandlerFactory = lockHandlerFactory;
        _logger = logger;
    }

    // ... Lease Acquisition Methods documented below ...
}

```

#### B. Lease Acquisition Methods
The `IDistributedLockHandler` exposes three methods for lease management:

| Method | Behavior | Use Case |
| --- | --- | --- |
| **`TryAcquireLockAsync`** | **Non-Blocking:** Attempts to acquire the lock once. Returns `null` immediately if the lock is held. | Ideal for time-critical jobs that should skip if busy (like the middleware). |
| **`WaitForAcquireLockAsync(TimeSpan)`** | **Blocking/Timeout:** Retries acquisition using exponential backoff until the lock is available or the timeout is reached. | Long-running orchestrators or background processes where work must eventually complete. |
| **`WaitForAcquireLockAsync(CancellationToken)`** | **Blocking/Token:** Retries acquisition indefinitely until the lock is acquired or the provided cancellation token is triggered. | Use when the calling context (e.g., HTTP request) dictates the timeout/cancellation. |

---

### C. Usage Examples
#### 1. TryAcquireLockAsync (Non-Blocking)
```csharp
public async Task ProcessData(string key)
{
    var handler = _lockHandlerFactory.CreateHandler();
    
    // Attempt to get the lock immediately.
    await using var handle = await handler.TryAcquireLockAsync(key);

    if (handle == null)
    {
        _logger.LogWarning($"Lock '{key}' held. Skipping processing.");
        return;
    }

    // Lock acquired. Automatic renewal is active.
    _logger.LogInformation("Lock acquired. Processing...");
    await Task.Delay(5000); 
    
    // Lock is released when the 'await using' block exits.
}

```

#### 2. WaitForAcquireLockAsync (With Timeout)
```csharp
public async Task RunMigrationScript(TimeSpan maxWait)
{
    const string lockKey = "GlobalMigrationLock";
    var handler = _lockHandlerFactory.CreateHandler();
    
    _logger.LogInformation($"Waiting up to {maxWait.TotalSeconds} seconds for lock...");

    // Retry acquisition for up to 5 minutes.
    await using var handle = await handler.WaitForAcquireLockAsync(lockKey, maxWait);

    if (handle == null)
    {
        _logger.LogError("Timeout reached. Could not acquire lock. Aborting.");
        return;
    }
    
    _logger.LogInformation("Lock acquired. Running critical migration.");
    // ... Critical logic ...
}

```