using Microsoft.Extensions.Logging;
using NSubstitute;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class MetadataImportServiceTests
{
    [TestMethod]
    public void FromJson_AllowsMissingOrganizerFields()
    {
        const string json = """
            {
              "ModelName": "Sample",
              "Hashes": {
                "BLAKE3": "hash"
              }
            }
            """;

        var result = ConnectedModelInfo.FromJson(json);

        Assert.IsNotNull(result);
        Assert.AreEqual("Sample", result.ModelName);
        Assert.IsNull(result.AuthorUsername);
        Assert.IsNull(result.RemoteFileName);
        Assert.IsNull(result.RemoteFileId);
    }

    [TestMethod]
    public async Task UpdateExistingMetadata_MergesUserFieldsAndBackfillsRemoteFields()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            var existingInfo = new ConnectedModelInfo
            {
                ModelName = "Old Name",
                Hashes = new CivitFileHashes { BLAKE3 = "blake3-hash" },
                ImportedAt = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero),
                UserTitle = "Pinned Name",
                ThumbnailImageUrl = Path.Combine(tempRoot, "existing.preview.png"),
                InferenceDefaults = new InferenceDefaults { Steps = 30, CfgScale = 7 },
                Source = ConnectedModelSource.Civitai,
            };
            await existingInfo.SaveJsonToDirectory(tempRoot, "model");

            var service = CreateMetadataImportService();
            ConfigureCivitLookup(service.Api, "blake3-hash");

            await service.Instance.UpdateExistingMetadata(new DirectoryPath(tempRoot));

            var updated = ConnectedModelInfo.FromJson(
                await File.ReadAllTextAsync(Path.Combine(tempRoot, "model.cm-info.json"))
            );

            Assert.IsNotNull(updated);
            Assert.AreEqual("Remote Model", updated.ModelName);
            Assert.AreEqual("Pinned Name", updated.UserTitle);
            Assert.AreEqual(existingInfo.ThumbnailImageUrl, updated.ThumbnailImageUrl);
            Assert.AreEqual(existingInfo.ImportedAt, updated.ImportedAt);
            Assert.IsNotNull(updated.InferenceDefaults);
            Assert.AreEqual(30, updated.InferenceDefaults.Steps);
            Assert.AreEqual("creator-name", updated.AuthorUsername);
            Assert.AreEqual("remote-file.safetensors", updated.RemoteFileName);
            Assert.AreEqual(321, updated.RemoteFileId);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public async Task GetMetadataForFile_ForceReimport_PersistsDownloadedThumbnailPath()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            var modelPath = Path.Combine(tempRoot, "model.safetensors");
            await File.WriteAllTextAsync(modelPath, "small model file");

            var hash = await FileHash.GetBlake3Async(new FilePath(modelPath));
            var service = CreateMetadataImportService();
            ConfigureCivitLookup(service.Api, hash, includeImage: true);
            service
                .DownloadService.DownloadToFileAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<IProgress<ProgressReport>?>(),
                    Arg.Any<string?>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(callInfo =>
                {
                    File.WriteAllText(callInfo.ArgAt<string>(1), "preview");
                    return Task.CompletedTask;
                });

            var result = await service.Instance.GetMetadataForFile(
                new FilePath(modelPath),
                forceReimport: true
            );

            Assert.IsNotNull(result);
            var expectedPreviewPath = Path.Combine(tempRoot, "model.preview.png");
            Assert.AreEqual(expectedPreviewPath, result.ThumbnailImageUrl);
            Assert.IsTrue(File.Exists(expectedPreviewPath));

            var saved = ConnectedModelInfo.FromJson(
                await File.ReadAllTextAsync(Path.Combine(tempRoot, "model.cm-info.json"))
            );
            Assert.IsNotNull(saved);
            Assert.AreEqual(expectedPreviewPath, saved.ThumbnailImageUrl);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sm-metadata-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void ConfigureCivitLookup(ICivitApi api, string hash, bool includeImage = false)
    {
        var file = new CivitFile
        {
            Id = 321,
            Name = "remote-file.safetensors",
            DownloadUrl = "https://example.invalid/model",
            Type = CivitFileType.Model,
            Metadata = new CivitFileMetadata { Size = "pruned" },
            Hashes = new CivitFileHashes { BLAKE3 = hash, SHA256 = "sha256" },
        };
        var version = new CivitModelVersion
        {
            Id = 456,
            Name = "Version One",
            BaseModel = "SDXL",
            Files = [file],
            Images = includeImage
                ? [new CivitImage { Url = "https://example.invalid/preview.png", Type = "image" }]
                : [],
            TrainedWords = ["tag-a"],
        };
        var model = new CivitModel
        {
            Id = 123,
            Name = "Remote Model",
            Type = CivitModelType.Checkpoint,
            Tags = ["tag-a"],
            Creator = new CivitCreator { Username = "creator-name" },
            ModelVersions = [version],
            Stats = new CivitModelStats(),
        };

        api.GetModelVersionByHash(hash)
            .Returns(
                Task.FromResult(
                    new CivitModelVersionResponse(
                        version.Id,
                        model.Id,
                        version.Name,
                        version.BaseModel!,
                        [file],
                        version.Images ?? [],
                        file.DownloadUrl
                    )
                )
            );
        api.GetModelById(model.Id).Returns(Task.FromResult(model));
    }

    private static (
        MetadataImportService Instance,
        ICivitApi Api,
        IDownloadService DownloadService
    ) CreateMetadataImportService()
    {
        var api = Substitute.For<ICivitApi>();
        var db = Substitute.For<ILiteDbContext>();
        var logger = Substitute.For<ILogger<MetadataImportService>>();
        var downloadService = Substitute.For<IDownloadService>();
        var finder = new ModelFinder(db, api);
        return (new MetadataImportService(logger, downloadService, finder), api, downloadService);
    }
}
