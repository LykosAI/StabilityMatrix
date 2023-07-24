using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class AnsiParserTests
{
    [DataTestMethod]
    [DataRow("\u001b[0m", "\u001b[0m")]
    [DataRow("\u001b[A", "\u001b[A")]
    [DataRow("\u001b[A\r\n", "\u001b[A")]
    public void TestAnsiRegex(string source, string expectedMatch)
    {
        var pattern = AnsiParser.AnsiEscapeSequenceRegex();
        var match = pattern.Match(source);
        Assert.IsTrue(match.Success);
        Assert.AreEqual(expectedMatch, match.Value);
    }
}
