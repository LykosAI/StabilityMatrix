using System.Diagnostics.CodeAnalysis;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Tests.Helper;

[TestClass]
public class EventManagerTests
{
    private EventManager eventManager = null!;
        
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
}
