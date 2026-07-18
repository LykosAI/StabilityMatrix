using System.Text.Json;
using StabilityMatrix.Core.Models.Api.HuggingFace;

namespace StabilityMatrix.Tests.Models;

[TestClass]
public class HuggingFaceTreeEntryTests
{
    [TestMethod]
    public void Sha256_FromTreeApiLfsOid_IsBareHex()
    {
        // The tree API returns the LFS oid as a bare hex digest (no "sha256:" prefix).
        const string json = """
            {
              "type": "file",
              "oid": "abc123gitsha1",
              "size": 1226977424,
              "path": "high_noise_model.safetensors",
              "lfs": { "oid": "d176c808d6fc461999b68e321efcb7501b20b8c3797523ed0df14f7d1deff11e", "size": 1226977424 }
            }
            """;

        var entry = JsonSerializer.Deserialize<HuggingFaceTreeEntry>(json);

        Assert.IsNotNull(entry);
        Assert.AreEqual("d176c808d6fc461999b68e321efcb7501b20b8c3797523ed0df14f7d1deff11e", entry.Sha256);
    }

    [TestMethod]
    public void Sha256_WithPointerStylePrefix_StripsPrefix()
    {
        var entry = new HuggingFaceTreeEntry { Lfs = new HuggingFaceLfsInfo { Oid = "sha256:deadbeef" } };
        Assert.AreEqual("deadbeef", entry.Sha256);
    }

    [TestMethod]
    public void Sha256_NonLfsFile_IsNull()
    {
        // Small files committed directly to git have no lfs entry; oid is the git blob SHA-1.
        var entry = new HuggingFaceTreeEntry { Oid = "gitblobsha1" };
        Assert.IsNull(entry.Sha256);
    }
}
