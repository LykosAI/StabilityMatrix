using System.Net;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Refit;
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
            // A linked model (has an id): remote fields, including the name, get refreshed
            var existingInfo = new ConnectedModelInfo
            {
                ModelId = 123,
                ModelName = "Old Name",
                Hashes = new CivitFileHashes { BLAKE3 = "blake3-hash" },
                ImportedAt = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero),
                UserTitle = "Pinned Name",
                ThumbnailImageUrl = Path.Combine(tempRoot, "existing.preview.png"),
                InferenceDefaults = new InferenceDefaults { Steps = 30, CfgScale = 7 },
                Tags = ["user-tag", "TAG-A"],
                Source = ConnectedModelSource.Civitai,
            };
            await existingInfo.SaveJsonToDirectory(tempRoot, "model");

            var service = CreateMetadataImportService();
            ConfigureCivitLookup(service.Api, "blake3-hash");

            var scanResult = await service.Instance.UpdateExistingMetadata(new DirectoryPath(tempRoot));

            Assert.AreEqual(1, scanResult.Found);
            Assert.IsFalse(scanResult.Aborted);

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
            CollectionAssert.AreEquivalent(new[] { "tag-a", "user-tag" }, updated.Tags);
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

            Assert.AreEqual(MetadataFetchStatus.Found, result.Status);
            Assert.IsNotNull(result.Metadata);
            var expectedPreviewPath = Path.Combine(tempRoot, "model.preview.png");
            Assert.AreEqual(expectedPreviewPath, result.Metadata.ThumbnailImageUrl);
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

    [TestMethod]
    public async Task UpdateExistingMetadata_HashMissing_RehashesModelFile()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            var modelPath = Path.Combine(tempRoot, "model.safetensors");
            await File.WriteAllTextAsync(modelPath, "small model file");
            var hash = await FileHash.GetBlake3Async(new FilePath(modelPath));

            // e.g. a HuggingFace / OpenModelDb import, which records only SHA256
            var existingInfo = new ConnectedModelInfo
            {
                ModelName = "Old Name",
                Hashes = new CivitFileHashes { SHA256 = "sha256-only" },
                Source = ConnectedModelSource.OpenModelDb,
            };
            await existingInfo.SaveJsonToDirectory(tempRoot, "model");

            var service = CreateMetadataImportService();
            ConfigureCivitLookup(service.Api, hash);

            var scanResult = await service.Instance.UpdateExistingMetadata(new DirectoryPath(tempRoot));

            Assert.AreEqual(1, scanResult.Found);
            Assert.AreEqual(0, scanResult.Skipped);

            var updated = ConnectedModelInfo.FromJson(
                await File.ReadAllTextAsync(Path.Combine(tempRoot, "model.cm-info.json"))
            );
            Assert.IsNotNull(updated);
            Assert.AreEqual("Remote Model", updated.ModelName);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public async Task ScanDirectoryForMissingInfo_RateLimited_AbortsEarly()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            for (var i = 0; i < 5; i++)
            {
                await File.WriteAllTextAsync(Path.Combine(tempRoot, $"model{i}.safetensors"), $"file {i}");
            }

            var service = CreateMetadataImportService();
            service
                .Api.GetModelVersionByHash(Arg.Any<string>())
                .Returns<Task<CivitModelVersionResponse>>(_ =>
                    throw CreateApiException(HttpStatusCode.TooManyRequests).Result
                );

            var scanResult = await service.Instance.ScanDirectoryForMissingInfo(new DirectoryPath(tempRoot));

            Assert.IsTrue(scanResult.Aborted);
            Assert.AreEqual(3, scanResult.Failed);
            Assert.AreEqual(0, scanResult.Found);
            await service.Api.Received(3).GetModelVersionByHash(Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public async Task ScanDirectoryForMissingInfo_NotFound_CountsWithoutAborting()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            for (var i = 0; i < 4; i++)
            {
                await File.WriteAllTextAsync(Path.Combine(tempRoot, $"model{i}.safetensors"), $"file {i}");
            }

            var service = CreateMetadataImportService();
            service
                .Api.GetModelVersionByHash(Arg.Any<string>())
                .Returns<Task<CivitModelVersionResponse>>(_ =>
                    throw CreateApiException(HttpStatusCode.NotFound).Result
                );

            var scanResult = await service.Instance.ScanDirectoryForMissingInfo(new DirectoryPath(tempRoot));

            Assert.IsFalse(scanResult.Aborted);
            Assert.AreEqual(4, scanResult.NotFound);
            Assert.AreEqual(0, scanResult.Found);
            await service.Api.Received(4).GetModelVersionByHash(Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public async Task ScanDirectoryForMissingInfo_CategoryOnlyAndCustomSidecarsAreEligible_ConnectedAndOptedOutAreNot()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            // bare, category-only, custom-named, connected, opted-out
            for (var i = 0; i < 5; i++)
                await File.WriteAllTextAsync(Path.Combine(tempRoot, $"model{i}.safetensors"), $"file {i}");

            await new ConnectedModelInfo
            {
                Source = null,
                User = new UserModelMetadata { Categories = ["Style"] },
            }.SaveJsonToDirectory(tempRoot, "model1");
            await new ConnectedModelInfo
            {
                ModelName = "My custom name",
                ModelDescription = "mine",
                Nsfw = true,
            }.SaveJsonToDirectory(tempRoot, "model2");
            await new ConnectedModelInfo { ModelId = 7, ModelName = "Linked" }.SaveJsonToDirectory(
                tempRoot,
                "model3"
            );
            await new ConnectedModelInfo
            {
                ModelName = "Detached",
                Source = null,
                User = new UserModelMetadata { ExcludeFromMetadataSearch = true },
            }.SaveJsonToDirectory(tempRoot, "model4");

            var service = CreateMetadataImportService();
            ConfigureCivitLookupForAnyHash(service.Api);

            var scanResult = await service.Instance.ScanDirectoryForMissingInfo(new DirectoryPath(tempRoot));

            Assert.AreEqual(3, scanResult.Total, "bare + category-only + custom are eligible");
            Assert.AreEqual(3, scanResult.Found);

            var categoryOnly = ConnectedModelInfo.FromJson(
                await File.ReadAllTextAsync(Path.Combine(tempRoot, "model1.cm-info.json"))
            );
            Assert.IsTrue(categoryOnly!.IsConnected);
            Assert.AreEqual("Remote Model", categoryOnly.ModelName);
            CollectionAssert.AreEqual(new[] { "Style" }, categoryOnly.User!.Categories);

            var custom = ConnectedModelInfo.FromJson(
                await File.ReadAllTextAsync(Path.Combine(tempRoot, "model2.cm-info.json"))
            );
            Assert.IsTrue(custom!.IsConnected);
            Assert.AreEqual("My custom name", custom.ModelName, "user-typed name survives the match");
            Assert.AreEqual("mine", custom.ModelDescription);
            Assert.IsTrue(custom.Nsfw);
            Assert.AreEqual("creator-name", custom.AuthorUsername, "remote fields still arrive");

            var optedOut = ConnectedModelInfo.FromJson(
                await File.ReadAllTextAsync(Path.Combine(tempRoot, "model4.cm-info.json"))
            );
            Assert.IsFalse(optedOut!.IsConnected);
            Assert.AreEqual("Detached", optedOut.ModelName);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public async Task GetMetadataForFile_ExplicitRequest_RunsForOptedOutModelAndClearsOptOut()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "model.safetensors"), "file");
            await new ConnectedModelInfo
            {
                ModelName = "Detached",
                Source = null,
                User = new UserModelMetadata { Categories = ["A"], ExcludeFromMetadataSearch = true },
            }.SaveJsonToDirectory(tempRoot, "model");

            var service = CreateMetadataImportService();
            ConfigureCivitLookupForAnyHash(service.Api);

            var result = await service.Instance.GetMetadataForFile(
                new FilePath(Path.Combine(tempRoot, "model.safetensors"))
            );

            Assert.AreEqual(MetadataFetchStatus.Found, result.Status);
            var onDisk = ConnectedModelInfo.FromJson(
                await File.ReadAllTextAsync(Path.Combine(tempRoot, "model.cm-info.json"))
            );
            Assert.IsTrue(onDisk!.IsConnected);
            Assert.IsFalse(onDisk.User!.ExcludeFromMetadataSearch);
            CollectionAssert.AreEqual(new[] { "A" }, onDisk.User.Categories);
            Assert.AreEqual("Detached", onDisk.ModelName, "user-typed name survives");
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public async Task UpdateExistingMetadata_SkipsOptedOutModels()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "model.safetensors"), "file");
            await new ConnectedModelInfo
            {
                ModelName = "Detached",
                Source = null,
                User = new UserModelMetadata { ExcludeFromMetadataSearch = true },
            }.SaveJsonToDirectory(tempRoot, "model");

            var service = CreateMetadataImportService();

            var scanResult = await service.Instance.UpdateExistingMetadata(new DirectoryPath(tempRoot));

            Assert.AreEqual(0, scanResult.Total);
            await service.Api.DidNotReceive().GetModelVersionByHash(Arg.Any<string>());
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    private static async Task<ApiException> CreateApiException(HttpStatusCode statusCode)
    {
        using var response = new HttpResponseMessage(statusCode);
        return await ApiException.Create(
            new HttpRequestMessage(HttpMethod.Get, "https://example.invalid"),
            HttpMethod.Get,
            response,
            new RefitSettings()
        );
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sm-metadata-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Like <see cref="ConfigureCivitLookup"/> but matches whatever hash the service computes.</summary>
    private static void ConfigureCivitLookupForAnyHash(ICivitApi api)
    {
        // The finder matches the requested hash against the returned model's files, so the
        // stub echoes whatever hash it was asked for.
        var lastHash = "unset";
        api.GetModelVersionByHash(Arg.Any<string>())
            .Returns(call =>
            {
                lastHash = call.Arg<string>();
                var (file, version, model) = BuildRemote(lastHash, includeImage: false);
                return Task.FromResult(
                    new CivitModelVersionResponse(
                        version.Id,
                        model.Id,
                        version.Name,
                        version.BaseModel!,
                        [file],
                        version.Images ?? [],
                        file.DownloadUrl
                    )
                );
            });
        api.GetModelById(Arg.Any<int>()).Returns(_ => Task.FromResult(BuildRemote(lastHash, false).Model));
    }

    private static void ConfigureCivitLookup(ICivitApi api, string hash, bool includeImage = false)
    {
        var (file, version, model) = BuildRemote(hash, includeImage);

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

    private static (CivitFile File, CivitModelVersion Version, CivitModel Model) BuildRemote(
        string hash,
        bool includeImage
    )
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

        return (file, version, model);
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
