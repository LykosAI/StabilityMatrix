using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class UserModelMetadataTests
{
    [TestMethod]
    public void ToJson_FromJson_RoundTripsCategories()
    {
        var metadata = new UserModelMetadata { Categories = ["Character", "Anime"] };

        var result = UserModelMetadata.FromJson(metadata.ToJson());

        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(new[] { "Character", "Anime" }, result.Categories);
        Assert.AreEqual(metadata, result);
    }

    [TestMethod]
    public void FromJson_MissingCategories_YieldsEmptyMetadata()
    {
        var result = UserModelMetadata.FromJson("{}");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsEmpty);
    }

    [TestMethod]
    public void Equals_IsOrderSensitiveAndCaseSensitive()
    {
        var a = new UserModelMetadata { Categories = ["Character", "Anime"] };
        var b = new UserModelMetadata { Categories = ["Anime", "Character"] };
        var c = new UserModelMetadata { Categories = ["character", "anime"] };

        Assert.AreNotEqual(a, b);
        Assert.AreNotEqual(a, c);
        Assert.AreEqual(a, new UserModelMetadata { Categories = ["Character", "Anime"] });
    }

    [TestMethod]
    public void GetSidecarPath_ReplacesModelExtension()
    {
        var modelPath = Path.Combine("root", "Lora", "foo.safetensors");

        var result = UserModelMetadata.GetSidecarPath(modelPath);

        Assert.AreEqual(Path.Combine("root", "Lora", "foo.cm-user.json"), result);
    }

    [TestMethod]
    public void LocalModelFile_Categories_EmptyWithoutMetadata()
    {
        var file = new LocalModelFile
        {
            RelativePath = @"Lora\foo.safetensors",
            SharedFolderType = SharedFolderType.Lora,
        };

        Assert.AreEqual(0, file.Categories.Count);

        file.UserMetadata = new UserModelMetadata { Categories = ["Style"] };

        CollectionAssert.AreEqual(new[] { "Style" }, file.Categories.ToList());
    }

    [TestMethod]
    public void LocalModelFile_Equality_IncludesUserMetadata()
    {
        var a = new LocalModelFile
        {
            RelativePath = @"Lora\foo.safetensors",
            SharedFolderType = SharedFolderType.Lora,
        };
        var b = a with { UserMetadata = new UserModelMetadata { Categories = ["Style"] } };

        Assert.AreNotEqual(a, b);
        Assert.IsFalse(LocalModelFile.RelativePathConnectedModelInfoComparer.Equals(a, b));
    }

    [TestMethod]
    public void LocalModelFile_GetDeleteFullPaths_IncludesUserSidecar()
    {
        var root = Path.Combine(Path.GetTempPath(), "sm-tests", Guid.NewGuid().ToString("N"));
        var loraDir = Path.Combine(root, "Lora");
        Directory.CreateDirectory(loraDir);

        try
        {
            File.WriteAllText(Path.Combine(loraDir, "foo.safetensors"), "");
            File.WriteAllText(Path.Combine(loraDir, "foo.cm-user.json"), "{}");

            var file = new LocalModelFile
            {
                RelativePath = Path.Combine("Lora", "foo.safetensors"),
                SharedFolderType = SharedFolderType.Lora,
            };

            var paths = file.GetDeleteFullPaths(root).ToList();

            CollectionAssert.Contains(paths, Path.Combine(loraDir, "foo.cm-user.json"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
