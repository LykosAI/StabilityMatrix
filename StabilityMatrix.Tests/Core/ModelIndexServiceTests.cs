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
    public void GetHasEarlyAccessUpdateOnly_TrackAware_IgnoresOtherArchitectures()
    {
        // Mirrors civitai model 1377820: the installed Illustrious file's real update (v7,
        // early access) must not be masked by a newer public release for another architecture
        var model = CreateLocalModel(installedVersionId: 100, hasUpdate: true, baseModel: "Illustrious");
        var remoteModel = CreateRemoteModel(
            CreateVersion(id: 300, isEarlyAccess: true, baseModel: "Illustrious"),
            CreateVersion(id: 250, isEarlyAccess: false, baseModel: "Anima"),
            CreateVersion(id: 100, isEarlyAccess: false, baseModel: "Illustrious")
        );

        var result = InvokeGetHasEarlyAccessUpdateOnly(model, remoteModel);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ComputeHasUpdate_IgnoresCrossArchitectureReleases()
    {
        var model = CreateLocalModel(installedVersionId: 100, hasUpdate: false, baseModel: "Illustrious");
        var remoteModel = CreateRemoteModel(
            CreateVersion(id: 250, isEarlyAccess: false, "Anima", CreateFile(CivitFileType.Model, "anima1")),
            CreateVersion(
                id: 100,
                isEarlyAccess: false,
                "Illustrious",
                CreateFile(CivitFileType.Model, "illu6")
            )
        );

        var result = InvokeComputeHasUpdate(model, remoteModel, ["illu6"]);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ComputeHasUpdate_FlagsSameTrackUpdate()
    {
        var model = CreateLocalModel(installedVersionId: 100, hasUpdate: false, baseModel: "Illustrious");
        var remoteModel = CreateRemoteModel(
            CreateVersion(
                id: 300,
                isEarlyAccess: true,
                "Illustrious",
                CreateFile(CivitFileType.Model, "illu7")
            ),
            CreateVersion(id: 250, isEarlyAccess: false, "Anima", CreateFile(CivitFileType.Model, "anima1")),
            CreateVersion(
                id: 100,
                isEarlyAccess: false,
                "Illustrious",
                CreateFile(CivitFileType.Model, "illu6")
            )
        );

        var result = InvokeComputeHasUpdate(model, remoteModel, ["illu6"]);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsEarlyAccess_TrueForFutureDeadline_DespitePublicAvailability()
    {
        // Current CivitAI responses report availability "Public" for early-access versions
        // and signal the state via earlyAccessDeadline instead (observed on model 1377820)
        var version = CreateVersion(id: 100, isEarlyAccess: false);
        version.Availability = "Public";
        version.EarlyAccessDeadline = DateTimeOffset.UtcNow.AddDays(7);

        Assert.IsTrue(version.IsEarlyAccess);
    }

    [TestMethod]
    public void IsEarlyAccess_FalseForPastDeadline()
    {
        var version = CreateVersion(id: 100, isEarlyAccess: false);
        version.Availability = "Public";
        version.EarlyAccessDeadline = DateTimeOffset.UtcNow.AddDays(-1);

        Assert.IsFalse(version.IsEarlyAccess);
    }

    [TestMethod]
    public void IsEarlyAccess_TrueForExplicitAvailability()
    {
        var version = CreateVersion(id: 100, isEarlyAccess: true);

        Assert.IsTrue(version.IsEarlyAccess);
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

    private static LocalModelFile CreateLocalModel(
        int installedVersionId,
        bool hasUpdate,
        string? baseModel = null
    )
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
                BaseModel = baseModel,
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
        return CreateVersion(id, isEarlyAccess, baseModel: null, files);
    }

    private static CivitModelVersion CreateVersion(
        int id,
        bool isEarlyAccess,
        string? baseModel,
        params CivitFile[] files
    )
    {
        return new CivitModelVersion
        {
            Id = id,
            Name = $"v{id}",
            Description = string.Empty,
            DownloadUrl = string.Empty,
            TrainedWords = [],
            Availability = isEarlyAccess ? "EarlyAccess" : "Public",
            BaseModel = baseModel,
            Stats = new CivitModelStats(),
            Files = files.Length > 0 ? files.ToList() : null,
        };
    }
}
