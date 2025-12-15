using System.Reflection;
using Azure.Storage.Blobs;
using Escapement.Azure.Functions.DistributedLock.Handlers;
using Escapement.Azure.Functions.DistributedLock.Middleware;
using Microsoft.Azure.Functions.Worker;
using Moq;

namespace Escapement.Azure.Functions.DistributedLock.Test.Middleware
{
  public class DistributedLockMiddlewareResolveTemplateTests
  {
    private DistributedLockMiddleware CreateMiddleware()
    {
      var mockBlobService = new Mock<BlobServiceClient>(MockBehavior.Strict);
      // Return a loose BlobContainerClient so constructor's CreateIfNotExists call is harmless
      mockBlobService.Setup(s => s.GetBlobContainerClient(It.IsAny<string>())).Returns(new Mock<BlobContainerClient>().Object);

      return new DistributedLockMiddleware(new BlobLeaseHandlerFactory(mockBlobService.Object));
    }

    private string InvokeResolveTemplate(DistributedLockMiddleware middleware, string template, IDictionary<string, object> bindingData)
    {
      var mockContext = new Mock<FunctionContext>();
      var mockBindingContext = new Mock<BindingContext>();
      var readOnly = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object>(
        new Dictionary<string, object>(bindingData ?? new Dictionary<string, object>())
      );
      mockBindingContext.SetupGet(b => b.BindingData).Returns(readOnly);
      mockContext.SetupGet(c => c.BindingContext).Returns(mockBindingContext.Object);

      var method = typeof(DistributedLockMiddleware).GetMethod("ResolveTemplate", BindingFlags.NonPublic | BindingFlags.Instance);
      Assert.NotNull(method);

      var result = method.Invoke(middleware, [template, mockContext.Object]) as string;
      return result ?? string.Empty;
    }

    [Fact]
    public void ResolveTemplate_StaticName_ReturnsSanitizedLowercase()
    {
      var middleware = CreateMiddleware();
      var resolved = InvokeResolveTemplate(middleware, "MyStaticLockName", new Dictionary<string, object> { { "Id", 123 } });

      // Middleware lower-cases names
      Assert.Equal("mystaticlockname", resolved);
    }

    [Fact]
    public void ResolveTemplate_TemplateWithId_ReplacesPlaceholder()
    {
      var middleware = CreateMiddleware();
      var resolved = InvokeResolveTemplate(middleware, "order-{Id}", new Dictionary<string, object> { { "Id", 123 } });

      Assert.Equal("order-123", resolved);
    }

    [Fact]
    public void ResolveTemplate_TemplateWithRegion_SanitizesSpacesAndLowercases()
    {
      var middleware = CreateMiddleware();
      var resolved = InvokeResolveTemplate(middleware, "{Region}-sync", new Dictionary<string, object> { { "Region", "West US" } });

      Assert.Equal("west-us-sync", resolved);
    }

    [Fact]
    public void ResolveTemplate_TemplateWithoutPlaceholder_KeepsConstant()
    {
      var middleware = CreateMiddleware();
      var resolved = InvokeResolveTemplate(middleware, "constant", new Dictionary<string, object> { { "Id", 123 } });

      Assert.Equal("constant", resolved);
    }
  }
}
