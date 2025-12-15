using Escapement.Azure.Functions.DistributedLock.Extensions;
using Escapement.Azure.Functions.DistributedLock.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Escapement.Azure.Functions.DistributedLock.Middleware
{
  public class DistributedLockMiddleware : IFunctionsWorkerMiddleware
  {
    private readonly IDistributedLockHandlerFactory _lockHandlerFactory;

    // Dependency injection now uses the factory interface
    public DistributedLockMiddleware(IDistributedLockHandlerFactory lockHandlerFactory)
    {
      _lockHandlerFactory = lockHandlerFactory;
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
      await using var lockHandle = await lockHandler.TryAcquireLockAsync(resolvedLockKey);

      if (lockHandle == null)
      {
        var logger = context.GetLogger<DistributedLockMiddleware>();
        logger.LogWarning("Singleton lock {LockKey} is already held. Skipping execution.", resolvedLockKey);
        return; // Skip execution if lock not acquired
      }

      // 3. Lock acquired, proceed with function execution
      await next(context);
    }

    private string ResolveTemplate(string template, FunctionContext context)
    {
      if (string.IsNullOrEmpty(template)) return "default-lock";

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
  }
}
