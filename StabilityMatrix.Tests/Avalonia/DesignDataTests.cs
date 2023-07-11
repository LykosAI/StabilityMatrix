using System.Reflection;
using StabilityMatrix.Avalonia.DesignData;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class DesignDataTests
{
    [TestInitialize]
    public void TestInitialize()
    {
        // invoke static ctor by property access
        Assert.IsNotNull(StabilityMatrix.Avalonia.DesignData.DesignData.LaunchPageViewModel);
    }
    
    // Return all properties
    public static IEnumerable<object[]> DesignDataProperties => 
        typeof(StabilityMatrix.Avalonia.DesignData.DesignData).GetProperties()
            .Select(p => new object[] { p });
    
    [TestMethod]
    [DynamicData(nameof(DesignDataProperties))]
    public void Property_ShouldBeNotNull(PropertyInfo property)
    {
        var value = property.GetValue(null);
        Assert.IsNotNull(value);
    }
}
