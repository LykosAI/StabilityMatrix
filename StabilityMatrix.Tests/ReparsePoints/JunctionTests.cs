using System.Runtime.InteropServices;
using StabilityMatrix.Core.ReparsePoints;

namespace StabilityMatrix.Tests.ReparsePoints;

using System.IO;


[TestClass]
public class JunctionTest
{
    private string tempFolder = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Inconclusive("Test cannot be run on anything but Windows currently.");
            return;
        }
        
        tempFolder = Path.GetTempFileName();
        File.Delete(tempFolder);
        Directory.CreateDirectory(tempFolder);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (string.IsNullOrEmpty(tempFolder)) return;
        TempFiles.DeleteDirectory(tempFolder);
    }

    [TestMethod]
    public void Exists_NoSuchFile()
    {
        Assert.IsFalse(Junction.Exists(Path.Combine(tempFolder, "$$$NoSuchFolder$$$")));
    }

    [TestMethod]
    public void Exists_IsADirectory()
    {
        File.Create(Path.Combine(tempFolder, "AFile")).Close();

        Assert.IsFalse(Junction.Exists(Path.Combine(tempFolder, "AFile")));
    }

    [TestMethod]
    public void Create_VerifyExists_GetTarget_Delete()
    {
        var targetFolder = Path.Combine(tempFolder, "ADirectory");
        var junctionPoint = Path.Combine(tempFolder, "SymLink");

        Directory.CreateDirectory(targetFolder);
        File.Create(Path.Combine(targetFolder, "AFile")).Close();

        // Verify behavior before junction point created.
        Assert.IsFalse(File.Exists(Path.Combine(junctionPoint, "AFile")),
            "File should not be located until junction point created.");

        Assert.IsFalse(Junction.Exists(junctionPoint), "Junction point not created yet.");

        // Create junction point and confirm its properties.
        Junction.Create(junctionPoint, targetFolder, false /*don't overwrite*/);

        Assert.IsTrue(Junction.Exists(junctionPoint), "Junction point exists now.");

        Assert.AreEqual(targetFolder, Junction.GetTarget(junctionPoint));

        Assert.IsTrue(File.Exists(Path.Combine(junctionPoint, "AFile")),
            "File should be accessible via the junction point.");

        // Delete junction point.
        Junction.Delete(junctionPoint);

        Assert.IsFalse(Junction.Exists(junctionPoint), "Junction point should not exist now.");

        Assert.IsFalse(File.Exists(Path.Combine(junctionPoint, "AFile")),
            "File should not be located after junction point deleted.");

        Assert.IsFalse(Directory.Exists(junctionPoint), "Ensure directory was deleted too.");

        // Cleanup
        File.Delete(Path.Combine(targetFolder, "AFile"));
    }

    [TestMethod]
    [ExpectedException(typeof(IOException), "Directory already exists and overwrite parameter is false.")]
    public void Create_ThrowsIfOverwriteNotSpecifiedAndDirectoryExists()
    {
        var targetFolder = Path.Combine(tempFolder, "ADirectory");
        var junctionPoint = Path.Combine(tempFolder, "SymLink");

        Directory.CreateDirectory(junctionPoint);

        Junction.Create(junctionPoint, targetFolder, false);
    }

    [TestMethod]
    public void Create_OverwritesIfSpecifiedAndDirectoryExists()
    {
        var targetFolder = Path.Combine(tempFolder, "ADirectory");
        var junctionPoint = Path.Combine(tempFolder, "SymLink");

        Directory.CreateDirectory(junctionPoint);
        Directory.CreateDirectory(targetFolder);

        Junction.Create(junctionPoint, targetFolder, true);

        Assert.AreEqual(targetFolder, Junction.GetTarget(junctionPoint));
    }

    [TestMethod]
    [ExpectedException(typeof(IOException), "Target path does not exist or is not a directory.")]
    public void Create_ThrowsIfTargetDirectoryDoesNotExist()
    {
        var targetFolder = Path.Combine(tempFolder, "ADirectory");
        var junctionPoint = Path.Combine(tempFolder, "SymLink");

        Junction.Create(junctionPoint, targetFolder, false);
    }

    [TestMethod]
    [ExpectedException(typeof(IOException), "Unable to open reparse point.")]
    public void GetTarget_NonExistentJunctionPoint()
    {
        Junction.GetTarget(Path.Combine(tempFolder, "SymLink"));
    }

    [TestMethod]
    [ExpectedException(typeof(IOException), "Path is not a junction point.")]
    public void GetTarget_CalledOnADirectoryThatIsNotAJunctionPoint()
    {
        Junction.GetTarget(tempFolder);
    }

    [TestMethod]
    [ExpectedException(typeof(IOException), "Path is not a junction point.")]
    public void GetTarget_CalledOnAFile()
    {
        File.Create(Path.Combine(tempFolder, "AFile")).Close();

        Junction.GetTarget(Path.Combine(tempFolder, "AFile"));
    }

    [TestMethod]
    public void Delete_NonExistentJunctionPoint()
    {
        // Should do nothing.
        Junction.Delete(Path.Combine(tempFolder, "SymLink"));
    }

    [TestMethod]
    [ExpectedException(typeof(IOException), "Unable to delete junction point.")]
    public void Delete_CalledOnADirectoryThatIsNotAJunctionPoint()
    {
        Junction.Delete(tempFolder);
    }

    [TestMethod]
    [ExpectedException(typeof(IOException), "Path is not a junction point.")]
    public void Delete_CalledOnAFile()
    {
        File.Create(Path.Combine(tempFolder, "AFile")).Close();

        Junction.Delete(Path.Combine(tempFolder, "AFile"));
    }
}
