using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using StabilityMatrix.Models.Packages;

namespace StabilityMatrix.Tests.Helper;

[TestClass]
public class PackageFactoryTests
{
    private PackageFactory packageFactory;
    private IEnumerable<BasePackage> fakeBasePackages;
    
    [TestInitialize]
    public void Setup()
    {
        fakeBasePackages = new List<BasePackage>
        {
            // TODO: inject mocks
            new DankDiffusion(null, null)
        };
        packageFactory = new PackageFactory(fakeBasePackages);
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
