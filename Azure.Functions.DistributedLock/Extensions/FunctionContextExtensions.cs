using Microsoft.Azure.Functions.Worker;
using System.Reflection;

namespace Azure.Functions.DistributedLock.Extensions
{
  public static class FunctionContextExtensions
  {
    public static MethodInfo? GetTargetMethod(this FunctionContext context)
    {
      // Guard against null FunctionDefinition or missing data
      if (context?.FunctionDefinition == null) return null;

      // The EntryPoint string is usually "Namespace.Class.Method"
      var entryPoint = context.FunctionDefinition.EntryPoint;
      var assemblyPath = context.FunctionDefinition.PathToAssembly;

      if (string.IsNullOrEmpty(entryPoint) || string.IsNullOrEmpty(assemblyPath)) return null;

      // Split the entry point to get the type and method name
      var lastDotIndex = entryPoint.LastIndexOf('.');
      if (lastDotIndex < 0) return null;
      var typeName = entryPoint.Substring(0, lastDotIndex);
      var methodName = entryPoint.Substring(lastDotIndex + 1);

      Type? type = null;

      // If the path points to an existing file, try loading that assembly
      try
      {
        if (!string.IsNullOrEmpty(assemblyPath) && System.IO.File.Exists(assemblyPath))
        {
          var assembly = Assembly.LoadFrom(assemblyPath);
          type = assembly.GetType(typeName);
        }
      }
      catch
      {
        // Ignore load failures and fall back to searching loaded assemblies
      }

      if (type == null)
      {
        // Try to find the type in already loaded assemblies
        type = AppDomain.CurrentDomain.GetAssemblies()
          .Select(a => a.GetType(typeName))
          .FirstOrDefault(t => t != null);
      }

      // Prefer the parameterless overload if available to avoid ambiguous matches
      return type?.GetMethod(methodName, Type.EmptyTypes) ?? type?.GetMethod(methodName);
    }
  }
}
