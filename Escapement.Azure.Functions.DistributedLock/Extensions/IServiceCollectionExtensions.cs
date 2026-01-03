using Azure.Storage.Blobs;
using Escapement.Azure.Functions.DistributedLock.Handlers;
using Escapement.Azure.Functions.DistributedLock.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Escapement.Azure.Functions.DistributedLock
{
  public static class IServiceCollectionExtensions
  {
    /// <summary>
    /// Add distributed lock services using Azure Blob Storage leases.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="connectionString">BlobServiceClient connectionstring, defaults to using environment variable "AzureWebJobsStorage"</param>
    /// <returns></returns>
    public static IServiceCollection AddBlobServiceDistributedLock(this IServiceCollection services, string? connectionString = null)
    {
      services.AddSingleton(sp => new BlobServiceClient(connectionString ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")));
      return services.AddSingleton<IDistributedLockHandlerFactory, BlobLeaseHandlerFactory>();
    }
  }
}
