using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Tests.Helper;

[TestClass]
public class PackageFactoryTests
{
    private PackageFactory packageFactory = null!;
    private IEnumerable<BasePackage> fakeBasePackages = null!;

    [TestInitialize]
    public void Setup()
    {
        fakeBasePackages = new List<BasePackage>
        {
            new DankDiffusion(null!, null!, null!, null!, null!, null!),
        };
        packageFactory = new PackageFactory(
            fakeBasePackages,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!
        );
    }

    [TestMethod]
    public void GetAllAvailablePackages_ReturnsAllPackages()
    {
        var result = packageFactory.GetAllAvailablePackages();
        Assert.AreEqual(1, result.Count());
    }

    [TestMethod]
    public void FindPackageByName_ReturnsPackage()
    {
        var result = packageFactory.FindPackageByName("dank-diffusion");
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void FindPackageByName_ReturnsNull()
    {
        var result = packageFactory.FindPackageByName("not-a-package");
        Assert.IsNull(result);
    }
}
