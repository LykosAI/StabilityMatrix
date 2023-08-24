using System.Reflection;
using StabilityMatrix.Avalonia.DesignData;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class DesignDataTests
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
        DesignData.Initialize();
    }
    
    // Return all properties
    public static IEnumerable<object[]> DesignDataProperties => 
        typeof(DesignData).GetProperties()
            .Select(p => new object[] { p });
    
    [TestMethod]
    [DynamicData(nameof(DesignDataProperties))]
    public void Property_ShouldBeNotNull(PropertyInfo property)
    {
        var value = property.GetValue(null);
        Assert.IsNotNull(value);
    }
}
