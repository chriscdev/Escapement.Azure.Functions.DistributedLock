using Escapement.Azure.Functions.DistributedLock.Attributes;

namespace Escapement.Azure.Functions.DistributedLock.Test.Attributes
{
  public class DistributedLockAttributeTests
  {
    [Fact]
    public void DistributedLockAttribute_StoresLockKey()
    {
      var attr = new DistributedLockAttribute("my-key");
      Assert.Equal("my-key", attr.LockKey);
    }
  }
}
