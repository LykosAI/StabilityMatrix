using System.Reflection;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class ModelIndexServiceTests
{
    [TestMethod]
    public void GetHasEarlyAccessUpdateOnly_ReturnsTrue_WhenAllNewerVersionsAreEarlyAccess()
    {
        var model = CreateLocalModel(installedVersionId: 100, hasUpdate: true);
        var remoteModel = CreateRemoteModel(
            CreateVersion(id: 300, isEarlyAccess: true),
            CreateVersion(id: 200, isEarlyAccess: true),
            CreateVersion(id: 100, isEarlyAccess: false)
        );

        var result = InvokeGetHasEarlyAccessUpdateOnly(model, remoteModel);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void GetHasEarlyAccessUpdateOnly_ReturnsFalse_WhenAnyNewerVersionIsPublic()
    {
        var model = CreateLocalModel(installedVersionId: 100, hasUpdate: true);
        var remoteModel = CreateRemoteModel(
            CreateVersion(id: 300, isEarlyAccess: true),
            CreateVersion(id: 200, isEarlyAccess: false),
            CreateVersion(id: 100, isEarlyAccess: false)
        );

        var result = InvokeGetHasEarlyAccessUpdateOnly(model, remoteModel);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void GetHasEarlyAccessUpdateOnly_ReturnsFalse_WhenInstalledVersionIsLatest()
    {
        var model = CreateLocalModel(installedVersionId: 100, hasUpdate: true);
        var remoteModel = CreateRemoteModel(
            CreateVersion(id: 100, isEarlyAccess: false),
            CreateVersion(id: 90, isEarlyAccess: true)
        );

        var result = InvokeGetHasEarlyAccessUpdateOnly(model, remoteModel);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void GetHasEarlyAccessUpdateOnly_ReturnsFalse_WhenModelHasNoUpdate()
    {
        var model = CreateLocalModel(installedVersionId: 100, hasUpdate: false);
        var remoteModel = CreateRemoteModel(
            CreateVersion(id: 300, isEarlyAccess: true),
            CreateVersion(id: 200, isEarlyAccess: true),
            CreateVersion(id: 100, isEarlyAccess: false)
        );

        var result = InvokeGetHasEarlyAccessUpdateOnly(model, remoteModel);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void GetHasEarlyAccessUpdateOnly_ReturnsFalse_WhenInstalledVersionIsNotInRemoteList()
    {
        var model = CreateLocalModel(installedVersionId: 100, hasUpdate: true);
        var remoteModel = CreateRemoteModel(
            CreateVersion(id: 300, isEarlyAccess: true),
            CreateVersion(id: 200, isEarlyAccess: true),
            CreateVersion(id: 150, isEarlyAccess: false)
        );

        var result = InvokeGetHasEarlyAccessUpdateOnly(model, remoteModel);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void GetHasEarlyAccessUpdateOnly_ReturnsTrue_WhenInstalledVersionMissingAndAllVersionsAreEarlyAccess()
    {
        var model = CreateLocalModel(installedVersionId: 100, hasUpdate: true);
        var remoteModel = CreateRemoteModel(
            CreateVersion(id: 300, isEarlyAccess: true),
            CreateVersion(id: 200, isEarlyAccess: true)
        );

        var result = InvokeGetHasEarlyAccessUpdateOnly(model, remoteModel);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ComputeHasUpdate_ReturnsFalse_WhenLatestNonWeightFileIsInstalled()
    {
        // Embeddings/VAEs/upscalers publish files typed Negative/VAE/Upscaler rather than
        // Model — these must count as installable evidence, not produce a permanent badge
        var model = CreateLocalModel(installedVersionId: 100, hasUpdate: false);
        var remoteModel = CreateRemoteModel(
            CreateVersion(id: 100, isEarlyAccess: false, CreateFile(CivitFileType.Negative, "aabbcc"))
        );

        var result = InvokeComputeHasUpdate(model, remoteModel, ["aabbcc"]);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ComputeHasUpdate_ReturnsFalse_WhenInstalledVersionIsLatestDespiteNoHashes()
    {
        var model = CreateLocalModel(installedVersionId: 100, hasUpdate: false);
        var remoteModel = CreateRemoteModel(
            CreateVersion(id: 100, isEarlyAccess: false, CreateFile(CivitFileType.TrainingData, null))
        );

        var result = InvokeComputeHasUpdate(model, remoteModel, []);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ComputeHasUpdate_ReturnsTrue_WhenNoHashesAndInstalledVersionIsOlder()
    {
        var model = CreateLocalModel(installedVersionId: 100, hasUpdate: false);
        var remoteModel = CreateRemoteModel(
            CreateVersion(id: 200, isEarlyAccess: false),
            CreateVersion(id: 100, isEarlyAccess: false)
        );

        var result = InvokeComputeHasUpdate(model, remoteModel, []);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ComputeHasUpdate_ReturnsFalse_WhenNoHashesAndInstalledVersionUnknown()
    {
        var model = CreateLocalModel(installedVersionId: 100, hasUpdate: true);
        var remoteModel = CreateRemoteModel(
            CreateVersion(id: 300, isEarlyAccess: false),
            CreateVersion(id: 200, isEarlyAccess: false)
        );

        var result = InvokeComputeHasUpdate(model, remoteModel, []);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ComputeHasUpdate_ReturnsTrue_WhenLatestWeightsNotInstalled()
    {
        var model = CreateLocalModel(installedVersionId: 100, hasUpdate: false);
        var remoteModel = CreateRemoteModel(
            CreateVersion(id: 200, isEarlyAccess: false, CreateFile(CivitFileType.Model, "ddeeff")),
            CreateVersion(id: 100, isEarlyAccess: false, CreateFile(CivitFileType.Model, "aabbcc"))
        );

        var result = InvokeComputeHasUpdate(model, remoteModel, ["aabbcc"]);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ComputeHasUpdate_ReturnsFalse_WhenRemoteModelHasNoVersions()
    {
        var model = CreateLocalModel(installedVersionId: 100, hasUpdate: true);
        var remoteModel = CreateRemoteModel();

        var result = InvokeComputeHasUpdate(model, remoteModel, []);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void CollectModelHashes_MatchesCaseInsensitively()
    {
        var model = CreateLocalModel(installedVersionId: 100, hasUpdate: false);
        model.ConnectedModelInfo!.Hashes = new CivitFileHashes { BLAKE3 = "AABBCC" };

        var method = typeof(ModelIndexService).GetMethod(
            "CollectModelHashes",
            BindingFlags.NonPublic | BindingFlags.Static
        );

        Assert.IsNotNull(method);

        var hashes = (HashSet<string>)method.Invoke(null, [new[] { model }])!;

        Assert.IsTrue(hashes.Contains("aabbcc"));
    }

    private static bool InvokeComputeHasUpdate(
        LocalModelFile model,
        CivitModel remoteModel,
        string[] installedHashes
    )
    {
        var method = typeof(ModelIndexService).GetMethod(
            "ComputeHasUpdate",
            BindingFlags.NonPublic | BindingFlags.Static
        );

        Assert.IsNotNull(method);

        var result = method.Invoke(null, [model, remoteModel, new HashSet<string>(installedHashes)]);

        Assert.IsNotNull(result);

        return (bool)result;
    }

    private static CivitFile CreateFile(CivitFileType type, string? blake3)
    {
        return new CivitFile
        {
            Name = "file.safetensors",
            Type = type,
            Hashes = new CivitFileHashes { BLAKE3 = blake3 },
        };
    }

    private static bool InvokeGetHasEarlyAccessUpdateOnly(LocalModelFile model, CivitModel? remoteModel)
    {
        var method = typeof(ModelIndexService).GetMethod(
            "GetHasEarlyAccessUpdateOnly",
            BindingFlags.NonPublic | BindingFlags.Static
        );

        Assert.IsNotNull(method);

        var result = method.Invoke(null, [model, remoteModel]);

        Assert.IsNotNull(result);

        return (bool)result;
    }

    private static LocalModelFile CreateLocalModel(int installedVersionId, bool hasUpdate)
    {
        return new LocalModelFile
        {
            RelativePath = "StableDiffusion/test-model.safetensors",
            SharedFolderType = SharedFolderType.StableDiffusion,
            HasUpdate = hasUpdate,
            ConnectedModelInfo = new ConnectedModelInfo
            {
                ModelId = 123,
                VersionId = installedVersionId,
                Source = ConnectedModelSource.Civitai,
                ModelName = "Test Model",
                ModelDescription = string.Empty,
                VersionName = $"v{installedVersionId}",
                Tags = [],
                Hashes = new CivitFileHashes(),
            },
        };
    }

    private static CivitModel CreateRemoteModel(params CivitModelVersion[] versions)
    {
        return new CivitModel
        {
            Id = 123,
            Name = "Test Model",
            Description = string.Empty,
            Type = CivitModelType.Unknown,
            Tags = [],
            Stats = new CivitModelStats(),
            ModelVersions = versions.ToList(),
        };
    }

    private static CivitModelVersion CreateVersion(int id, bool isEarlyAccess, params CivitFile[] files)
    {
        return new CivitModelVersion
        {
            Id = id,
            Name = $"v{id}",
            Description = string.Empty,
            DownloadUrl = string.Empty,
            TrainedWords = [],
            Availability = isEarlyAccess ? "EarlyAccess" : "Public",
            Stats = new CivitModelStats(),
            Files = files.Length > 0 ? files.ToList() : null,
        };
    }
}
