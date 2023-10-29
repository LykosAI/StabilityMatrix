using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Tests.Models;

[TestClass]
public class ProcessArgsTests
{
    [DataTestMethod]
    [DataRow("pip", new[] { "pip" })]
    [DataRow("pip install torch", new[] { "pip", "install", "torch" })]
    [DataRow(
        "pip install -r \"file spaces/here\"",
        new[] { "pip", "install", "-r", "file spaces/here" }
    )]
    [DataRow(
        "pip install -r \"file spaces\\here\"",
        new[] { "pip", "install", "-r", "file spaces\\here" }
    )]
    public void TestStringToArray(string input, string[] expected)
    {
        ProcessArgs args = input;
        string[] result = args;
        CollectionAssert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow(new[] { "pip" }, "pip")]
    [DataRow(new[] { "pip", "install", "torch" }, "pip install torch")]
    [DataRow(
        new[] { "pip", "install", "-r", "file spaces/here" },
        "pip install -r \"file spaces/here\""
    )]
    [DataRow(
        new[] { "pip", "install", "-r", "file spaces\\here" },
        "pip install -r \"file spaces\\here\""
    )]
    public void TestArrayToString(string[] input, string expected)
    {
        ProcessArgs args = input;
        string result = args;
        Assert.AreEqual(expected, result);
    }
}
