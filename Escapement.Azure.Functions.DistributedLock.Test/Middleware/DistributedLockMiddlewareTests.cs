using Escapement.Azure.Functions.DistributedLock.Handlers;
using Escapement.Azure.Functions.DistributedLock.Middleware;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Moq;
using System.Reflection;

namespace Escapement.Azure.Functions.DistributedLock.Test.Middleware
{
  public class DistributedLockMiddlewareTests
  {
    [Fact]
    public async Task Invoke_WhenNoAttribute_CallsNext()
    {
      var mockBlobService = new Mock<BlobServiceClient>(MockBehavior.Strict);
      mockBlobService.Setup(s => s.GetBlobContainerClient(It.IsAny<string>())).Returns(new Mock<BlobContainerClient>().Object);

      var middleware = new DistributedLockMiddleware(new BlobLeaseHandlerFactory(mockBlobService.Object));

      var mockContext = new Mock<FunctionContext>();
      var didCallNext = false;
      FunctionExecutionDelegate next = ctx => { didCallNext = true; return Task.CompletedTask; };

      // FunctionDefinition on context will be null in our mocked context, so middleware should call next.
      await middleware.Invoke(mockContext.Object, next);

      Assert.True(didCallNext);
    }

    [Fact]
    public async Task Invoke_WhenFunctionDefinitionHasNoEntryPoint_CallsNext()
    {
      var mockBlobService = new Mock<BlobServiceClient>(MockBehavior.Strict);
      mockBlobService.Setup(s => s.GetBlobContainerClient(It.IsAny<string>())).Returns(new Mock<BlobContainerClient>().Object);

      var middleware = new DistributedLockMiddleware(new BlobLeaseHandlerFactory(mockBlobService.Object));

      var mockContext = new Mock<FunctionContext>();
      var mockFuncDef = new Mock<FunctionDefinition>();
      mockFuncDef.SetupGet(f => f.EntryPoint).Returns((string?)null);
      mockContext.SetupGet(c => c.FunctionDefinition).Returns(mockFuncDef.Object);

      var didCallNext = false;
      FunctionExecutionDelegate next = ctx => { didCallNext = true; return Task.CompletedTask; };

      await middleware.Invoke(mockContext.Object, next);

      Assert.True(didCallNext);
    }

    [Fact]
    public async Task Invoke_WhenEntryPointNotResolvable_CallsNext()
    {
      var mockBlobService = new Mock<BlobServiceClient>(MockBehavior.Strict);
      mockBlobService.Setup(s => s.GetBlobContainerClient(It.IsAny<string>())).Returns(new Mock<BlobContainerClient>().Object);

      var middleware = new DistributedLockMiddleware(new BlobLeaseHandlerFactory(mockBlobService.Object));

      var mockContext = new Mock<FunctionContext>();
      var mockFuncDef = new Mock<FunctionDefinition>();
      // Point to a non-existent type so GetTargetMethod will return null
      mockFuncDef.SetupGet(f => f.EntryPoint).Returns("Non.Existent.Type.Method");
      mockFuncDef.SetupGet(f => f.PathToAssembly).Returns(Assembly.GetExecutingAssembly().Location);
      mockContext.SetupGet(c => c.FunctionDefinition).Returns(mockFuncDef.Object);

      var didCallNext = false;
      FunctionExecutionDelegate next = ctx => { didCallNext = true; return Task.CompletedTask; };

      await middleware.Invoke(mockContext.Object, next);

      Assert.True(didCallNext);
    }
  }
}
