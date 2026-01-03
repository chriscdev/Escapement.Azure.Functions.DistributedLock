using Escapement.Azure.Functions.DistributedLock.Handlers;
using Escapement.Azure.Functions.DistributedLock.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Escapement.Azure.Functions.DistributedLock
{
  public static class IServiceCollectionExtensions
  {
    public static IServiceCollection AddDistributedLock(this IServiceCollection services)
    {
      return services.AddSingleton<IDistributedLockHandlerFactory, BlobLeaseHandlerFactory>();
    }
  }
}
