using Microsoft.Azure.Functions.Worker;
using Moq;
using Azure.Functions.DistributedLock.Extensions;

namespace Azure.Functions.DistributedLock.Test.Extensions
{
  public class FunctionContextExtensionsTests
  {
    [Fact]
    public void GetTargetMethod_ReturnsMethodInfo_WhenEntryPointIsValid()
    {
      // Arrange
      var mockDefinition = new Mock<FunctionDefinition>(MockBehavior.Strict);
      mockDefinition.SetupGet(d => d.EntryPoint).Returns("System.String.ToString");
      mockDefinition.SetupGet(d => d.PathToAssembly).Returns(typeof(string).Assembly.Location);

      var mockContext = new Mock<FunctionContext>();
      mockContext.SetupGet(c => c.FunctionDefinition).Returns(mockDefinition.Object);

      // Act
      var method = mockContext.Object.GetTargetMethod();

      // Assert
      Assert.NotNull(method);
      Assert.Equal("ToString", method!.Name);
    }
  }
}
