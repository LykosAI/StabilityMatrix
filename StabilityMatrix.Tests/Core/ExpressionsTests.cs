using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class ExpressionsTests
{
    private class TestClass
    {
        public int Id { get; set; }
        public NestedTestClass? Nested { get; set; }
    }
    
    private class NestedTestClass
    {
        public string Text { get; set; } = "";
    }
    
    [TestMethod]
    public void GetAssigner_Simple_PropertyName()
    {
        var (propertyName, _) =
            Expressions.GetAssigner<TestClass, int>(x => x.Id);
        
        // Check that the property name is correct
        Assert.AreEqual("Id", propertyName);
    }
    
    [TestMethod]
    public void GetAssigner_Simple_PropertyAssignment()
    {
        var obj = new TestClass();
        
        var (_, assigner) =
            Expressions.GetAssigner<TestClass, int>(x => x.Id);
        
        assigner.Compile()(obj, 42);
        
        Assert.AreEqual(42, obj.Id);
    }
}
