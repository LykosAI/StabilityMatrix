using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class CivitModelVersionInstallStateTests
{
    private static CivitFile File(CivitFileType type, string hash) =>
        new()
        {
            Type = type,
            Hashes = new CivitFileHashes { BLAKE3 = hash },
        };

    private static CivitModelVersion Version(params CivitFile[] files) => new() { Files = files.ToList() };

    private static CivitModelVersion MultiFileVersion() =>
        Version(
            File(CivitFileType.DiffusionModel, "bf16"),
            File(CivitFileType.DiffusionModel, "fp8"),
            File(CivitFileType.TextEncoder, "shared-encoder"),
            File(CivitFileType.VAE, "shared-vae")
        );

    [TestMethod]
    public void NothingInstalled_IsNotInstalled()
    {
        Assert.AreEqual(
            CivitModelVersionInstallState.NotInstalled,
            MultiFileVersion().GetInstallState(new HashSet<string> { "unrelated" })
        );
    }

    [TestMethod]
    public void OnlySharedComponentsInstalled_IsComponentsOnly()
    {
        Assert.AreEqual(
            CivitModelVersionInstallState.ComponentsOnly,
            MultiFileVersion().GetInstallState(new HashSet<string> { "shared-encoder", "shared-vae" })
        );
    }

    [TestMethod]
    public void AnyMainModelVariantInstalled_IsInstalled()
    {
        Assert.AreEqual(
            CivitModelVersionInstallState.Installed,
            MultiFileVersion().GetInstallState(new HashSet<string> { "fp8" })
        );
    }

    [TestMethod]
    public void UnknownFileTypeInstalled_IsInstalled()
    {
        var version = Version(File(CivitFileType.Unknown, "new-type"), File(CivitFileType.VAE, "vae"));

        Assert.AreEqual(
            CivitModelVersionInstallState.Installed,
            version.GetInstallState(new HashSet<string> { "new-type" })
        );
    }

    [TestMethod]
    public void VersionWithOnlyComponentFiles_IsInstalledWhenOneIsPresent()
    {
        var version = Version(File(CivitFileType.VAE, "vae"), File(CivitFileType.Config, "config"));

        Assert.AreEqual(
            CivitModelVersionInstallState.Installed,
            version.GetInstallState(new HashSet<string> { "vae" })
        );
    }
}
