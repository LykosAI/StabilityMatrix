using Avalonia.Controls.Notifications;
using NSubstitute;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class ModelImportServiceTests
{
    private static ModelImportService CreateService(
        IDownloadService downloadService,
        INotificationService notificationService,
        ITrackedDownloadService trackedDownloadService,
        IModelIndexService? modelIndexService = null,
        ISettingsManager? settingsManager = null
    ) =>
        new(
            downloadService,
            notificationService,
            trackedDownloadService,
            modelIndexService ?? Substitute.For<IModelIndexService>(),
            settingsManager ?? Substitute.For<ISettingsManager>()
        );

    [TestMethod]
    public async Task DoCustomImport_StartsTrackedDownloadBeforePreviewDownloadCompletes()
    {
        var downloadService = Substitute.For<IDownloadService>();
        var notificationService = Substitute.For<INotificationService>();
        var trackedDownloadService = Substitute.For<ITrackedDownloadService>();
        var service = CreateService(downloadService, notificationService, trackedDownloadService);

        var tempDir = Directory.CreateTempSubdirectory();
        var modelUri = new Uri("https://example.org/model.safetensors");
        var previewUri = new Uri("https://example.org/preview.webp");
        var previewDownload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var trackedDownload = new TrackedDownload
        {
            Id = Guid.NewGuid(),
            SourceUrl = modelUri,
            DownloadDirectory = new DirectoryPath(tempDir.FullName),
            FileName = "model.safetensors",
            TempFileName = "model.safetensors.tmp",
        };

        try
        {
            downloadService
                .DownloadToFileAsync(
                    previewUri.ToString(),
                    Arg.Any<string>(),
                    Arg.Any<IProgress<ProgressReport>?>(),
                    Arg.Any<string?>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(previewDownload.Task);

            notificationService
                .TryAsync(Arg.Any<Task>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<NotificationType>())
                .Returns(call => AwaitTask(call.Arg<Task>()));

            trackedDownloadService.NewDownload(modelUri, Arg.Any<FilePath>()).Returns(trackedDownload);
            trackedDownloadService.TryStartDownload(trackedDownload).Returns(Task.CompletedTask);

            var importTask = service.DoCustomImport(
                [modelUri],
                "model.safetensors",
                new DirectoryPath(tempDir.FullName),
                previewUri,
                ".webp"
            );

            var completedTask = await Task.WhenAny(importTask, Task.Delay(TimeSpan.FromSeconds(1)));

            Assert.AreSame(
                importTask,
                completedTask,
                "The model import should not wait for preview image download completion."
            );

            await trackedDownloadService.Received(1).TryStartDownload(trackedDownload);
            Assert.IsFalse(previewDownload.Task.IsCompleted);
        }
        finally
        {
            previewDownload.TrySetResult();
            tempDir.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task DoCustomImport_CreatesPatternSubfoldersAndDownloadsIntoThem()
    {
        var downloadService = Substitute.For<IDownloadService>();
        var notificationService = Substitute.For<INotificationService>();
        var trackedDownloadService = Substitute.For<ITrackedDownloadService>();
        var service = CreateService(downloadService, notificationService, trackedDownloadService);

        var tempDir = Directory.CreateTempSubdirectory();
        var modelUri = new Uri("https://example.org/model.safetensors");
        FilePath? capturedPath = null;

        try
        {
            trackedDownloadService
                .NewDownload(modelUri, Arg.Do<FilePath>(path => capturedPath = path))
                .Returns(call => new TrackedDownload
                {
                    Id = Guid.NewGuid(),
                    SourceUrl = modelUri,
                    DownloadDirectory = call.Arg<FilePath>().Directory!,
                    FileName = call.Arg<FilePath>().Name,
                    TempFileName = "model.safetensors.tmp",
                });

            await service.DoCustomImport(
                [modelUri],
                "SDXL/Creator/Model/model.safetensors",
                new DirectoryPath(tempDir.FullName)
            );

            var expectedDirectory = Path.Combine(tempDir.FullName, "SDXL", "Creator", "Model");
            Assert.IsTrue(Directory.Exists(expectedDirectory));
            Assert.IsNotNull(capturedPath);
            Assert.AreEqual(Path.Combine(expectedDirectory, "model.safetensors"), capturedPath.FullPath);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task DoCustomImport_DropsTraversalSegmentsFromPatternPath()
    {
        var downloadService = Substitute.For<IDownloadService>();
        var notificationService = Substitute.For<INotificationService>();
        var trackedDownloadService = Substitute.For<ITrackedDownloadService>();
        var service = CreateService(downloadService, notificationService, trackedDownloadService);

        var tempDir = Directory.CreateTempSubdirectory();
        var modelUri = new Uri("https://example.org/model.safetensors");
        FilePath? capturedPath = null;

        try
        {
            trackedDownloadService
                .NewDownload(modelUri, Arg.Do<FilePath>(path => capturedPath = path))
                .Returns(call => new TrackedDownload
                {
                    Id = Guid.NewGuid(),
                    SourceUrl = modelUri,
                    DownloadDirectory = call.Arg<FilePath>().Directory!,
                    FileName = call.Arg<FilePath>().Name,
                    TempFileName = "model.safetensors.tmp",
                });

            await service.DoCustomImport(
                [modelUri],
                "../../SDXL/model.safetensors",
                new DirectoryPath(tempDir.FullName)
            );

            Assert.IsNotNull(capturedPath);
            Assert.AreEqual(
                Path.Combine(tempDir.FullName, "SDXL", "model.safetensors"),
                capturedPath.FullPath
            );
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task DoImport_SkipsDownloadWhenDuplicateInstalled()
    {
        var downloadService = Substitute.For<IDownloadService>();
        var notificationService = Substitute.For<INotificationService>();
        var trackedDownloadService = Substitute.For<ITrackedDownloadService>();
        var modelIndexService = Substitute.For<IModelIndexService>();
        var settingsManager = Substitute.For<ISettingsManager>();
        var service = CreateService(
            downloadService,
            notificationService,
            trackedDownloadService,
            modelIndexService,
            settingsManager
        );

        var tempDir = Directory.CreateTempSubdirectory();

        try
        {
            // Indexed duplicate that exists on disk
            var installedRelativePath = Path.Combine("StableDiffusion", "installed.safetensors");
            var installedFullPath = Path.Combine(tempDir.FullName, installedRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(installedFullPath)!);
            await File.WriteAllTextAsync(installedFullPath, "weights");

            settingsManager.ModelsDirectory.Returns(tempDir.FullName);
            modelIndexService
                .FindByHashAsync("test-blake3")
                .Returns(
                    [
                        new LocalModelFile
                        {
                            RelativePath = installedRelativePath,
                            SharedFolderType = SharedFolderType.StableDiffusion,
                        },
                    ]
                );

            var canceledInvoked = false;

            await service.DoImport(
                new CivitModel(),
                new DirectoryPath(tempDir.FullName),
                selectedVersion: new CivitModelVersion(),
                selectedFile: new CivitFile
                {
                    Name = "model.safetensors",
                    Hashes = new CivitFileHashes { BLAKE3 = "test-blake3" },
                },
                onImportCanceled: () =>
                {
                    canceledInvoked = true;
                    return Task.CompletedTask;
                },
                duplicateHandling: DuplicateDownloadHandling.Skip
            );

            await trackedDownloadService.DidNotReceive().TryStartDownload(Arg.Any<TrackedDownload>());
            Assert.IsTrue(canceledInvoked, "Skipping a duplicate should invoke the cancel callback");
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task DoImport_AllowBypassesDuplicateCheck()
    {
        var downloadService = Substitute.For<IDownloadService>();
        var notificationService = Substitute.For<INotificationService>();
        var trackedDownloadService = Substitute.For<ITrackedDownloadService>();
        var modelIndexService = Substitute.For<IModelIndexService>();
        var settingsManager = Substitute.For<ISettingsManager>();
        var service = CreateService(
            downloadService,
            notificationService,
            trackedDownloadService,
            modelIndexService,
            settingsManager
        );

        var tempDir = Directory.CreateTempSubdirectory();

        try
        {
            // Installed duplicate present on disk, but Allow must not even consult the index
            var installedRelativePath = Path.Combine("StableDiffusion", "installed.safetensors");
            var installedFullPath = Path.Combine(tempDir.FullName, installedRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(installedFullPath)!);
            await File.WriteAllTextAsync(installedFullPath, "weights");

            settingsManager.ModelsDirectory.Returns(tempDir.FullName);
            modelIndexService
                .FindByHashAsync("test-blake3")
                .Returns(
                    [
                        new LocalModelFile
                        {
                            RelativePath = installedRelativePath,
                            SharedFolderType = SharedFolderType.StableDiffusion,
                        },
                    ]
                );

            trackedDownloadService
                .NewDownload(Arg.Any<string>(), Arg.Any<FilePath>())
                .Returns(call => new TrackedDownload
                {
                    Id = Guid.NewGuid(),
                    SourceUrl = new Uri(call.Arg<string>()),
                    DownloadDirectory = call.Arg<FilePath>().Directory!,
                    FileName = call.Arg<FilePath>().Name,
                    TempFileName = "model.safetensors.tmp",
                });

            await service.DoImport(
                new CivitModel(),
                new DirectoryPath(tempDir.FullName),
                selectedVersion: new CivitModelVersion(),
                selectedFile: new CivitFile
                {
                    Name = "model.safetensors",
                    DownloadUrl = "https://example.org/model.safetensors",
                    Hashes = new CivitFileHashes { BLAKE3 = "test-blake3" },
                },
                duplicateHandling: DuplicateDownloadHandling.Allow
            );

            await trackedDownloadService.Received(1).TryStartDownload(Arg.Any<TrackedDownload>());
            await modelIndexService.DidNotReceive().FindByHashAsync(Arg.Any<string>());
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task DoImport_ProceedsWhenIndexedDuplicateMissingOnDisk()
    {
        var downloadService = Substitute.For<IDownloadService>();
        var notificationService = Substitute.For<INotificationService>();
        var trackedDownloadService = Substitute.For<ITrackedDownloadService>();
        var modelIndexService = Substitute.For<IModelIndexService>();
        var settingsManager = Substitute.For<ISettingsManager>();
        var service = CreateService(
            downloadService,
            notificationService,
            trackedDownloadService,
            modelIndexService,
            settingsManager
        );

        var tempDir = Directory.CreateTempSubdirectory();

        try
        {
            // Indexed duplicate whose file no longer exists — stale entry must not block
            settingsManager.ModelsDirectory.Returns(tempDir.FullName);
            modelIndexService
                .FindByHashAsync("test-blake3")
                .Returns(
                    [
                        new LocalModelFile
                        {
                            RelativePath = Path.Combine("StableDiffusion", "deleted.safetensors"),
                            SharedFolderType = SharedFolderType.StableDiffusion,
                        },
                    ]
                );

            // NSubstitute intercepts the default-interface string overload too, so stub it directly
            trackedDownloadService
                .NewDownload(Arg.Any<string>(), Arg.Any<FilePath>())
                .Returns(call => new TrackedDownload
                {
                    Id = Guid.NewGuid(),
                    SourceUrl = new Uri(call.Arg<string>()),
                    DownloadDirectory = call.Arg<FilePath>().Directory!,
                    FileName = call.Arg<FilePath>().Name,
                    TempFileName = "model.safetensors.tmp",
                });

            await service.DoImport(
                new CivitModel(),
                new DirectoryPath(tempDir.FullName),
                selectedVersion: new CivitModelVersion(),
                selectedFile: new CivitFile
                {
                    Name = "model.safetensors",
                    DownloadUrl = "https://example.org/model.safetensors",
                    Hashes = new CivitFileHashes { BLAKE3 = "test-blake3" },
                }
            );

            await trackedDownloadService.Received(1).TryStartDownload(Arg.Any<TrackedDownload>());
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    private static async Task<TaskResult<bool>> AwaitTask(Task task)
    {
        await task;
        return new TaskResult<bool>(true);
    }
}
