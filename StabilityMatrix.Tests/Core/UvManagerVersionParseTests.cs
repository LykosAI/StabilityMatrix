using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class UvManagerVersionParseTests
{
    [TestMethod]
    public void ParseUvInstallDirVersion_CpythonRelease_ReturnsVersion()
    {
        var version = UvManager.ParseUvInstallDirVersion("cpython-3.12.10-windows-x86_64-none");

        Assert.IsNotNull(version);
        var v = version.Value;
        Assert.AreEqual(3, v.Major);
        Assert.AreEqual(12, v.Minor);
        Assert.AreEqual(10, v.Micro);
    }

    [TestMethod]
    public void ParseUvInstallDirVersion_Pypy_ReturnsVersion()
    {
        var version = UvManager.ParseUvInstallDirVersion("pypy-3.10.14-linux-x86_64-gnu");

        Assert.IsNotNull(version);
        var v = version.Value;
        Assert.AreEqual(3, v.Major);
        Assert.AreEqual(10, v.Minor);
    }

    [TestMethod]
    public void ParseUvInstallDirVersion_NoMicro_DefaultsToZero()
    {
        var version = UvManager.ParseUvInstallDirVersion("cpython-3.12");

        Assert.IsNotNull(version);
        var v = version.Value;
        Assert.AreEqual(3, v.Major);
        Assert.AreEqual(12, v.Minor);
        Assert.AreEqual(0, v.Micro);
    }

    [TestMethod]
    public void ParseUvInstallDirVersion_PrereleaseSuffix_ReturnsBaseVersion()
    {
        var version = UvManager.ParseUvInstallDirVersion("cpython-3.13.0rc1-linux-x86_64-gnu");

        Assert.IsNotNull(version);
        var v = version.Value;
        Assert.AreEqual(3, v.Major);
        Assert.AreEqual(13, v.Minor);
    }

    [TestMethod]
    public void ParseUvInstallDirVersion_FreethreadedSuffix_ReturnsBaseVersion()
    {
        var version = UvManager.ParseUvInstallDirVersion("cpython-3.13.0+freethreaded-linux-x86_64-gnu");

        Assert.IsNotNull(version);
        var v = version.Value;
        Assert.AreEqual(3, v.Major);
        Assert.AreEqual(13, v.Minor);
    }

    [TestMethod]
    public void ParseUvInstallDirVersion_UnexpectedName_ReturnsNull()
    {
        Assert.IsNull(UvManager.ParseUvInstallDirVersion("cpython-unknown"));
        Assert.IsNull(UvManager.ParseUvInstallDirVersion("not-a-uv-dir"));
    }
}
