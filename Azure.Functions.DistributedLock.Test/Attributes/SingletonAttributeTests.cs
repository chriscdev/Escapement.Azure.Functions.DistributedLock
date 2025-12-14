using Azure.Functions.DistributedLock.Attributes;

namespace Azure.Functions.DistributedLock.Test.Attributes
{
  public class SingletonAttributeTests
  {
    [Fact]
    public void SingletonAttribute_StoresLockKey()
    {
      var attr = new SingletonAttribute("singleton-key");
      Assert.Equal("singleton-key", attr.LockKey);
    }
  }
}
