using Avalonia.Controls.Notifications;
using NSubstitute;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class ModelImportServiceTests
{
    [TestMethod]
    public async Task DoCustomImport_StartsTrackedDownloadBeforePreviewDownloadCompletes()
    {
        var downloadService = Substitute.For<IDownloadService>();
        var notificationService = Substitute.For<INotificationService>();
        var trackedDownloadService = Substitute.For<ITrackedDownloadService>();
        var service = new ModelImportService(downloadService, notificationService, trackedDownloadService);

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
        var service = new ModelImportService(downloadService, notificationService, trackedDownloadService);

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
        var service = new ModelImportService(downloadService, notificationService, trackedDownloadService);

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

    private static async Task<TaskResult<bool>> AwaitTask(Task task)
    {
        await task;
        return new TaskResult<bool>(true);
    }
}
