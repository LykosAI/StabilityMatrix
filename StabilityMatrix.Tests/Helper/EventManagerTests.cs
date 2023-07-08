using StabilityMatrix.Core.Helper;
using StabilityMatrix.Helper;

namespace StabilityMatrix.Tests.Helper;

[TestClass]
public class EventManagerTests
{
    private EventManager eventManager; 
        
    [TestInitialize]
    public void TestInitialize()
    {
        eventManager = EventManager.Instance;
    }
    
    [TestMethod]
    public void GlobalProgressChanged_ShouldBeInvoked()
    {
        // Arrange
        var progress = 0;
        eventManager.GlobalProgressChanged += (sender, args) => progress = args;
        
        // Act
        eventManager.OnGlobalProgressChanged(100);
        
        // Assert
        Assert.AreEqual(100, progress);
    }
    
    [TestMethod]
    public void RequestPageChange_ShouldBeInvoked()
    {
        // Arrange
        var pageType = typeof(object);
        eventManager.PageChangeRequested += (sender, args) => pageType = args;
        
        // Act
        eventManager.RequestPageChange(typeof(int));
        
        // Assert
        Assert.AreEqual(typeof(int), pageType);
    }
}
