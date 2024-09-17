using System.Collections.Immutable;
using NSubstitute;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Tests.Models;

[TestClass]
public class ProcessArgsTests
{
    [DataTestMethod]
    [DataRow("pip", new[] { "pip" })]
    [DataRow("pip install torch", new[] { "pip", "install", "torch" })]
    [DataRow("pip install -r \"file spaces/here\"", new[] { "pip", "install", "-r", "file spaces/here" })]
    [DataRow("pip install -r \"file spaces\\here\"", new[] { "pip", "install", "-r", "file spaces\\here" })]
    public void TestStringToArray(string input, string[] expected)
    {
        // Implicit (string -> ProcessArgs)
        ProcessArgs args = input;

        var result = args.ToArgumentArray().Select(arg => arg.Value).ToArray();

        // Assert
        CollectionAssert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow(new[] { "pip" }, "pip")]
    [DataRow(new[] { "pip", "install", "torch" }, "pip install torch")]
    [DataRow(new[] { "pip", "install", "-r", "file spaces/here" }, "pip install -r \"file spaces/here\"")]
    [DataRow(new[] { "pip", "install", "-r", "file spaces\\here" }, "pip install -r \"file spaces\\here\"")]
    public void TestArrayToString(string[] input, string expected)
    {
        ProcessArgs args = input;
        string result = args;
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void TestIsQuoted()
    {
        // Arrange
        var args = new ProcessArgsBuilder(
            "-test",
            // This should be quoted (has space)
            "--arg 123",
            // Should not be quoted in result
            Argument.Quoted("--arg 123")
        ).ToProcessArgs();

        // Act
        var argString = args.ToString();

        // Assert
        Assert.AreEqual(argString, "-test \"--arg 123\" --arg 123");
    }
}
