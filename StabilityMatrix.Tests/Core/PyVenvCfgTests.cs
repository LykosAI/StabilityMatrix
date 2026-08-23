using System.Text;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class PyVenvCfgTests
{
    private static string[] Lines(string content) =>
        content.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

    [TestMethod]
    public void Set_WithDuplicateKeys_RewritesEveryMatch()
    {
        var cfg = PyVenvCfg.Parse(
            "home = cpython-3.12.10\nbase-prefix = cpython-3.12.10\nhome = cpython-3.13.12"
        );

        cfg["home"] = "/new/python";

        CollectionAssert.AreEqual(
            new[] { "home = /new/python", "base-prefix = cpython-3.12.10", "home = /new/python" },
            Lines(cfg.ToString())
        );
        Assert.AreEqual("/new/python", cfg["home"]);
    }

    [TestMethod]
    public void Set_ExistingKey_UpdatesInPlace()
    {
        var cfg = PyVenvCfg.Parse("home = old\nbase-prefix = x");

        cfg["home"] = "new";

        CollectionAssert.AreEqual(new[] { "home = new", "base-prefix = x" }, Lines(cfg.ToString()));
    }

    [TestMethod]
    public void Set_MissingKey_Appends()
    {
        var cfg = PyVenvCfg.Parse("home = /py");

        cfg["base-executable"] = "/py/bin/python";

        CollectionAssert.AreEqual(
            new[] { "home = /py", "base-executable = /py/bin/python" },
            Lines(cfg.ToString())
        );
    }

    [TestMethod]
    public void Set_MissingKey_WhenContentEndsWithNewline_NoBlankLine()
    {
        var cfg = PyVenvCfg.Parse("home = /py\n");

        cfg["base-executable"] = "/py/bin/python";

        CollectionAssert.AreEqual(
            new[] { "home = /py", "base-executable = /py/bin/python" },
            Lines(cfg.ToString())
        );
    }

    [TestMethod]
    public void Set_PreservesUnrelatedKeysInOrder()
    {
        var cfg = PyVenvCfg.Parse(
            "home = a\nbase-prefix = b\nprompt = c\nbase-exec-prefix = d\nbase-executable = e"
        );

        cfg["home"] = "z";

        CollectionAssert.AreEqual(
            new[]
            {
                "home = z",
                "base-prefix = b",
                "prompt = c",
                "base-exec-prefix = d",
                "base-executable = e",
            },
            Lines(cfg.ToString())
        );
    }

    [TestMethod]
    public void Parse_KeyWithoutSpaces_ReadsAndUpdates()
    {
        var cfg = PyVenvCfg.Parse("home=3.12");

        Assert.AreEqual("3.12", cfg["home"]);

        cfg["home"] = "3.14";
        Assert.AreEqual("home = 3.14", Lines(cfg.ToString())[0]);
    }

    [TestMethod]
    public void Parse_ValueContainingEquals_KeepsWholeValue()
    {
        var cfg = PyVenvCfg.Parse("home=C:\\Program Files=Python");

        Assert.AreEqual("C:\\Program Files=Python", cfg["home"]);
    }

    [TestMethod]
    public void Get_DuplicateKeys_IsLastWins()
    {
        var cfg = PyVenvCfg.Parse("home = first\nhome = second");

        Assert.AreEqual("second", cfg["home"]);
    }

    [TestMethod]
    public void Load_Utf16Encoded_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pyvenv-{Guid.NewGuid():N}.cfg");
        try
        {
            File.WriteAllText(path, "home = x\n", Encoding.Unicode);

            Assert.ThrowsException<InvalidDataException>(() => PyVenvCfg.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
