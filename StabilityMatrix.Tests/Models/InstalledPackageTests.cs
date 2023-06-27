using StabilityMatrix.Models;

namespace StabilityMatrix.Tests.Models;

[TestClass]
public class InstalledPackageTests
{
    [DataTestMethod]
    [DataRow("C:\\User\\AppData\\StabilityMatrix", "C:\\User\\Other", null)]
    [DataRow("C:\\Data", "D:\\Data\\abc", null)]
    [DataRow("C:\\Data", "C:\\Data\\abc", "abc")]
    [DataRow("C:\\User\\AppData\\StabilityMatrix", "C:\\User\\AppData\\StabilityMatrix\\Packages\\abc", "Packages\\abc")]
    public void TestGetSubPath(string relativeTo, string path, string? expected)
    {
        var result = InstalledPackage.GetSubPath(relativeTo, path);
        Assert.AreEqual(expected, result);
    }
}
