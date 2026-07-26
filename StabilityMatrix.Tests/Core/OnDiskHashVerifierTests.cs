using System.IO;
using System.Threading.Tasks;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class OnDiskHashVerifierTests
{
    private string tempDir = string.Empty;

    [TestInitialize]
    public void Init()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "smtest_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
    }

    // SHA256 of the bytes "hello" (lowercase hex).
    private const string HelloSha256 = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";

    [TestMethod]
    public async Task MatchesAsync_CorrectHash_ReturnsTrue()
    {
        var path = Path.Combine(tempDir, "model.safetensors");
        await File.WriteAllTextAsync(path, "hello");

        var verifier = new OnDiskHashVerifier();
        Assert.IsTrue(await verifier.MatchesAsync(path, HelloSha256));
        // Case-insensitive.
        Assert.IsTrue(await verifier.MatchesAsync(path, HelloSha256.ToUpperInvariant()));
    }

    [TestMethod]
    public async Task MatchesAsync_WrongHash_ReturnsFalse()
    {
        var path = Path.Combine(tempDir, "model.safetensors");
        await File.WriteAllTextAsync(path, "hello");

        var verifier = new OnDiskHashVerifier();
        Assert.IsFalse(await verifier.MatchesAsync(path, new string('0', 64)));
    }

    [TestMethod]
    public async Task MatchesAsync_MissingFile_ReturnsFalse()
    {
        var verifier = new OnDiskHashVerifier();
        Assert.IsFalse(await verifier.MatchesAsync(Path.Combine(tempDir, "nope.safetensors"), HelloSha256));
    }

    [TestMethod]
    public async Task MatchesAsync_CachesHash_SamePathDifferentExpectations()
    {
        // A path shared by several candidates is hashed once but compared against each expectation:
        // only the matching one returns true.
        var path = Path.Combine(tempDir, "high_noise_model.safetensors");
        await File.WriteAllTextAsync(path, "hello");

        var verifier = new OnDiskHashVerifier();
        Assert.IsFalse(await verifier.MatchesAsync(path, new string('a', 64)));
        Assert.IsTrue(await verifier.MatchesAsync(path, HelloSha256));
    }
}
