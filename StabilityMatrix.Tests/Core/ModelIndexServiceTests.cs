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

    private static CivitModelVersion CreateVersion(int id, bool isEarlyAccess)
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
        };
    }
}
