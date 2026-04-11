using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Models;

[TestClass]
public class TrackedDownloadTests
{
    private string tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
    }

    private TrackedDownload CreateDownload(IDownloadService downloadService)
    {
        var download = new TrackedDownload
        {
            Id = Guid.NewGuid(),
            SourceUrl = new Uri("https://example.com/model.safetensors"),
            DownloadDirectory = new DirectoryPath(tempDir),
            FileName = "model.safetensors",
            TempFileName = "model.safetensors.partial",
        };
        download.SetDownloadService(downloadService);
        return download;
    }

    // Resume() must proceed when the state is Pending (queued resume fix).

    [TestMethod]
    public async Task Resume_WhileInPendingState_SetsStateToWorking()
    {
        // Arrange – download service that blocks forever until cancelled.
        var downloadService = Substitute.For<IDownloadService>();
        downloadService
            .ResumeDownloadToFileAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<long>(),
                Arg.Any<IProgress<ProgressReport>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(callInfo =>
            {
                var ct = callInfo.Arg<CancellationToken>();
                return Task.Delay(Timeout.Infinite, ct);
            });

        var download = CreateDownload(downloadService);
        download.SetPending(); // Simulate being queued

        // Act
        download.Resume();

        // Assert – Resume() must not have returned early; state is now Working.
        Assert.AreEqual(ProgressState.Working, download.ProgressState);

        // Cleanup – cancel and wait for the task to finish.
        var cancelledTcs = new TaskCompletionSource();
        download.ProgressStateChanged += (_, state) =>
        {
            if (state == ProgressState.Cancelled)
                cancelledTcs.TrySetResult();
        };
        download.Cancel();
        await cancelledTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    // Sidecar files (.cm-info.json, preview image) must survive a failed
    // download so a manual retry can succeed without recreating them.

    [TestMethod]
    public async Task OnFailed_SidecarFilesPreservedForRetry()
    {
        // Arrange – create sidecar files that ModelImportService would have written.
        var sidecarPath = Path.Combine(tempDir, "model.cm-info.json");
        var previewPath = Path.Combine(tempDir, "model.preview.png");
        await File.WriteAllTextAsync(sidecarPath, "{}");
        await File.WriteAllTextAsync(previewPath, "PNG");

        // Download service that immediately throws a non-transient error
        // (InvalidOperationException is not an IOException/AuthenticationException,
        //  so it goes straight to Failed without any auto-retry attempts).
        var downloadService = Substitute.For<IDownloadService>();
        downloadService
            .ResumeDownloadToFileAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<long>(),
                Arg.Any<IProgress<ProgressReport>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.FromException(new InvalidOperationException("Simulated download error")));

        var download = CreateDownload(downloadService);
        download.ExtraCleanupFileNames.Add(sidecarPath);
        download.ExtraCleanupFileNames.Add(previewPath);

        var failedTcs = new TaskCompletionSource();
        download.ProgressStateChanged += (_, state) =>
        {
            if (state == ProgressState.Failed)
                failedTcs.TrySetResult();
        };

        // Act
        download.Start();
        await failedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert – sidecar files must still exist for a potential manual retry.
        Assert.IsTrue(File.Exists(sidecarPath), ".cm-info.json should be preserved after failure");
        Assert.IsTrue(File.Exists(previewPath), "Preview image should be preserved after failure");
    }

    [TestMethod]
    public async Task OnCancelled_SidecarFilesAreDeleted()
    {
        // Sidecar files should still be cleaned up when the user explicitly cancels.
        var sidecarPath = Path.Combine(tempDir, "model.cm-info.json");
        var previewPath = Path.Combine(tempDir, "model.preview.png");
        await File.WriteAllTextAsync(sidecarPath, "{}");
        await File.WriteAllTextAsync(previewPath, "PNG");

        var downloadService = Substitute.For<IDownloadService>();
        downloadService
            .ResumeDownloadToFileAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<long>(),
                Arg.Any<IProgress<ProgressReport>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(callInfo =>
            {
                var ct = callInfo.Arg<CancellationToken>();
                return Task.Delay(Timeout.Infinite, ct);
            });

        var download = CreateDownload(downloadService);
        download.ExtraCleanupFileNames.Add(sidecarPath);
        download.ExtraCleanupFileNames.Add(previewPath);

        var cancelledTcs = new TaskCompletionSource();
        download.ProgressStateChanged += (_, state) =>
        {
            if (state == ProgressState.Cancelled)
                cancelledTcs.TrySetResult();
        };

        download.Start();
        download.Cancel();
        await cancelledTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsFalse(File.Exists(sidecarPath), ".cm-info.json should be deleted on cancel");
        Assert.IsFalse(File.Exists(previewPath), "Preview image should be deleted on cancel");
    }
}
