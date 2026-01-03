using Escapement.Azure.Functions.DistributedLock.Extensions;
using Escapement.Azure.Functions.DistributedLock.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.RegularExpressions;
using Escapement.Azure.Functions.DistributedLock.Options;
using Escapement.Azure.Functions.DistributedLock.Exceptions;
using Microsoft.Extensions.Options;

namespace Escapement.Azure.Functions.DistributedLock.Middleware
{
  public class DistributedLockMiddleware : IFunctionsWorkerMiddleware
  {
    private readonly IDistributedLockHandlerFactory _lockHandlerFactory;
    private readonly IOptionsMonitor<DistributedLockOptions> _options;

    // Dependency injection now uses the factory interface and options
    public DistributedLockMiddleware(IDistributedLockHandlerFactory lockHandlerFactory, IOptionsMonitor<DistributedLockOptions> options)
    {
      _lockHandlerFactory = lockHandlerFactory;
      _options = options;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
      var targetMethod = context.GetTargetMethod();
      // Try to find either the legacy SingletonAttribute or the new DistributedLockAttribute
      var lockAttribute = targetMethod?.GetCustomAttributes()
        .OfType<IDistributedLockAttribute>()
        .FirstOrDefault();

      if (lockAttribute == null)
      {
        await next(context);
        return;
      }

      string resolvedLockKey = ResolveTemplate(lockAttribute.LockKey, context);

      // 1. Get the handler for this specific lock key
      var lockHandler = _lockHandlerFactory.CreateHandler(resolvedLockKey);

      // 2. Try to acquire the lock using the abstraction (using will dispose of the lock when it goes out of scope)
      var logger = context.GetLogger<DistributedLockMiddleware>();

      // Configure timeout from options, fallback to 30s if not set
      var waitTimeout = _options?.CurrentValue?.WaitTimeout ?? TimeSpan.FromSeconds(30);

      // Link the function cancellation token (if any) so we respect host shutdown
      var functionCancellation = GetFunctionCancellationToken(context);

      using var timeoutCts = new CancellationTokenSource(waitTimeout);
      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, functionCancellation);

      await using var lockHandle = await lockHandler.WaitForAcquireLockAsync(resolvedLockKey, linkedCts.Token);

      if (lockHandle == null)
      {
        logger.LogError("Lock acquisition timed out for {LockKey} after {Timeout}", resolvedLockKey, waitTimeout);
        throw new LockUnavailableException(resolvedLockKey);
      }


      // 3. Lock acquired, proceed with function execution
      await next(context);
    }

    private string ResolveTemplate(string template, FunctionContext context)
    {
      if (string.IsNullOrEmpty(template)) return context.FunctionDefinition.Name; // Default to function name if template is empty

      var result = template;
      var bindingData = context.BindingContext.BindingData;

      // Match {Key}
      var matches = Regex.Matches(template, @"\{(?<key>[^}]+)\}");

      // If no matches found, result remains the original template string (e.g., "foo")
      foreach (Match match in matches)
      {
        string placeholder = match.Groups["key"].Value;

        if (bindingData.TryGetValue(placeholder, out var value) && value != null)
        {
          // Convert the binding data value to string and replace the placeholder
          result = result.Replace($"{{{placeholder}}}", value.ToString());
        }
      }

      // Final Sanitization: Blob names cannot have certain characters
      return SanitizeBlobName(result);
    }

    private string SanitizeBlobName(string name)
    {
      // Replace invalid blob name characters with hyphens
      // Blob names allow alphanumeric, hyphens, and dots mostly.
      return Regex.Replace(name, @"[^a-zA-Z0-9\.\-]", "-").ToLowerInvariant();
    }

    private CancellationToken GetFunctionCancellationToken(FunctionContext context)
    {
      // Use reflection to avoid compile-time dependency on a specific FunctionContext shape
      var prop = context.GetType().GetProperty("CancellationToken", BindingFlags.Public | BindingFlags.Instance);
      if (prop != null && prop.PropertyType == typeof(CancellationToken))
      {
        var value = prop.GetValue(context);
        if (value is CancellationToken ct) return ct;
      }

      return CancellationToken.None;
    }
  }
}
