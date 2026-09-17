using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class UserModelMetadataTests
{
    [TestMethod]
    public void UserBlock_RoundTripsThroughCmInfoJson()
    {
        var info = new ConnectedModelInfo
        {
            ModelName = "Frieren",
            User = new UserModelMetadata { Categories = ["Character", "Anime"] },
        };

        var result = ConnectedModelInfo.FromJson(System.Text.Json.JsonSerializer.Serialize(info));

        Assert.IsNotNull(result?.User);
        CollectionAssert.AreEqual(new[] { "Character", "Anime" }, result.User.Categories);
        Assert.AreEqual(info, result);
    }

    [TestMethod]
    public void FromJson_SidecarWithoutUserBlock_HasNullUser()
    {
        var result = ConnectedModelInfo.FromJson("""{"ModelName":"Sample","Hashes":{}}""");

        Assert.IsNotNull(result);
        Assert.IsNull(result.User);
        Assert.AreEqual(
            0,
            new LocalModelFile
            {
                RelativePath = "x",
                SharedFolderType = SharedFolderType.Lora,
                ConnectedModelInfo = result,
            }
                .Categories
                .Count
        );
    }

    [TestMethod]
    public async Task SaveJsonToDirectory_KeepsUserBlockFromDisk_WhenSavingWithoutOne()
    {
        var root = CreateTempDirectory();
        try
        {
            var existing = new ConnectedModelInfo
            {
                ModelName = "Old",
                User = new UserModelMetadata { Categories = ["Style"], ExcludeFromMetadataSearch = true },
            };
            await existing.SaveJsonToDirectory(root, "model");

            // A remote writer builds a fresh object and never touches User
            var refreshed = new ConnectedModelInfo { ModelId = 42, ModelName = "Remote" };
            await refreshed.SaveJsonToDirectory(root, "model");

            var onDisk = ConnectedModelInfo.FromJson(
                await File.ReadAllTextAsync(Path.Combine(root, "model.cm-info.json"))
            );

            Assert.AreEqual("Remote", onDisk!.ModelName);
            CollectionAssert.AreEqual(new[] { "Style" }, onDisk.User!.Categories);
            Assert.IsTrue(onDisk.User.ExcludeFromMetadataSearch);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task SaveJsonToDirectory_ExplicitUserBlockWins()
    {
        var root = CreateTempDirectory();
        try
        {
            await new ConnectedModelInfo
            {
                ModelName = "Old",
                User = new UserModelMetadata { Categories = ["Style"] },
            }.SaveJsonToDirectory(root, "model");

            await new ConnectedModelInfo
            {
                ModelName = "Old",
                User = new UserModelMetadata(),
            }.SaveJsonToDirectory(root, "model");

            var onDisk = ConnectedModelInfo.FromJson(
                await File.ReadAllTextAsync(Path.Combine(root, "model.cm-info.json"))
            );

            Assert.AreEqual(0, onDisk!.User!.Categories.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void IsConnected_PerSourceIdentity()
    {
        // Custom metadata from the editor: Source defaults to Civitai but there is no id
        Assert.IsFalse(new ConnectedModelInfo { ModelName = "Custom" }.IsConnected);
        // Category-only sidecar
        Assert.IsFalse(
            new ConnectedModelInfo
            {
                Source = null,
                User = new UserModelMetadata { Categories = ["A"] },
            }.IsConnected
        );
        // Disconnected: everything cleared
        Assert.IsFalse(new ConnectedModelInfo { ModelName = "Kept", Source = null }.IsConnected);

        Assert.IsTrue(new ConnectedModelInfo { ModelId = 1 }.IsConnected);
        Assert.IsTrue(new ConnectedModelInfo { Source = ConnectedModelSource.OpenModelDb }.IsConnected);
        Assert.IsTrue(new ConnectedModelInfo { Source = ConnectedModelSource.CivArchive }.IsConnected);
        Assert.IsTrue(
            new ConnectedModelInfo
            {
                Source = ConnectedModelSource.HuggingFace,
                SourceUrl = "https://huggingface.co/x",
            }.IsConnected
        );
    }

    [TestMethod]
    public void HasCustomMetadata_And_IsEmpty()
    {
        Assert.IsTrue(new ConnectedModelInfo { ModelName = "Custom" }.HasCustomMetadata);
        Assert.IsFalse(new ConnectedModelInfo { ModelId = 1, ModelName = "Remote" }.HasCustomMetadata);

        Assert.IsTrue(new ConnectedModelInfo { Source = null }.IsEmpty);
        Assert.IsTrue(new ConnectedModelInfo { Source = null, User = new UserModelMetadata() }.IsEmpty);
        Assert.IsFalse(
            new ConnectedModelInfo
            {
                Source = null,
                User = new UserModelMetadata { Categories = ["A"] },
            }.IsEmpty
        );
        Assert.IsFalse(
            new ConnectedModelInfo
            {
                Source = null,
                User = new UserModelMetadata { ExcludeFromMetadataSearch = true },
            }.IsEmpty
        );
        Assert.IsFalse(new ConnectedModelInfo { ModelName = "Custom" }.IsEmpty);
        Assert.IsFalse(new ConnectedModelInfo { ModelId = 1 }.IsEmpty);
    }

    [TestMethod]
    public void LocalModelFile_ConnectedVersusMetadata()
    {
        var custom = new LocalModelFile
        {
            RelativePath = @"Lora\a.safetensors",
            SharedFolderType = SharedFolderType.Lora,
            ConnectedModelInfo = new ConnectedModelInfo { ModelName = "Custom", BaseModel = "Pony" },
        };
        Assert.IsTrue(custom.HasMetadata);
        Assert.IsFalse(custom.HasConnectedModel);
        Assert.IsNull(custom.HashSha256, "custom sidecars have no hashes; the index must not crash on them");
        Assert.IsNull(custom.HashBlake3);
        Assert.IsFalse(custom.HasCivitMetadata);
        Assert.AreEqual("Custom", custom.DisplayModelName);

        var linked = custom with
        {
            ConnectedModelInfo = new ConnectedModelInfo { ModelId = 5, ModelName = "Remote" },
        };
        Assert.IsTrue(linked.HasConnectedModel);
        Assert.IsTrue(linked.HasCivitMetadata);
    }

    [TestMethod]
    public void LocalModelFile_Equality_IncludesUserBlock()
    {
        var a = new LocalModelFile
        {
            RelativePath = @"Lora\foo.safetensors",
            SharedFolderType = SharedFolderType.Lora,
            ConnectedModelInfo = new ConnectedModelInfo { ModelId = 1, ModelName = "X" },
        };
        var b = a with
        {
            ConnectedModelInfo = new ConnectedModelInfo
            {
                ModelId = 1,
                ModelName = "X",
                User = new UserModelMetadata { Categories = ["Style"] },
            },
        };

        Assert.AreNotEqual(a, b);
        Assert.IsFalse(LocalModelFile.RelativePathConnectedModelInfoComparer.Equals(a, b));
    }

    [TestMethod]
    public void LocalModelFile_GetDeleteFullPaths_IncludesSidecarEvenWhenNotConnected()
    {
        var root = CreateTempDirectory();
        var loraDir = Path.Combine(root, "Lora");
        Directory.CreateDirectory(loraDir);

        try
        {
            File.WriteAllText(Path.Combine(loraDir, "foo.safetensors"), "");
            File.WriteAllText(Path.Combine(loraDir, "foo.cm-info.json"), "{}");

            var file = new LocalModelFile
            {
                RelativePath = Path.Combine("Lora", "foo.safetensors"),
                SharedFolderType = SharedFolderType.Lora,
                ConnectedModelInfo = new ConnectedModelInfo
                {
                    Source = null,
                    User = new UserModelMetadata { Categories = ["A"] },
                },
            };

            CollectionAssert.Contains(
                file.GetDeleteFullPaths(root).ToList(),
                Path.Combine(loraDir, "foo.cm-info.json")
            );
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "sm-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
