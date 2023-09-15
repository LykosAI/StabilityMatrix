using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Collections;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels.Progress;

[View(typeof(ProgressManagerPage))]
public partial class ProgressManagerViewModel : PageViewModelBase
{
    private readonly INotificationService notificationService;

    public override string Title => "Download Manager";
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.ArrowCircleDown, IsFilled = true };
    public AvaloniaList<ProgressItemViewModelBase> ProgressItems { get; } = new();

    [ObservableProperty]
    private bool isOpen;

    public ProgressManagerViewModel(
        ITrackedDownloadService trackedDownloadService,
        INotificationService notificationService
    )
    {
        this.notificationService = notificationService;

        // Attach to the event
        trackedDownloadService.DownloadAdded += TrackedDownloadService_OnDownloadAdded;
        EventManager.Instance.PackageInstallProgressAdded += InstanceOnPackageInstallProgressAdded;
        EventManager.Instance.ToggleProgressFlyout += (_, _) => IsOpen = !IsOpen;
    }

    private void InstanceOnPackageInstallProgressAdded(
        object? sender,
        IPackageModificationRunner runner
    )
    {
        AddPackageInstall(runner).SafeFireAndForget();
    }

    private void TrackedDownloadService_OnDownloadAdded(object? sender, TrackedDownload e)
    {
        var vm = new DownloadProgressItemViewModel(e);

        // Attach notification handlers
        e.ProgressStateChanged += (s, state) =>
        {
            var download = s as TrackedDownload;

            switch (state)
            {
                case ProgressState.Success:
                    Dispatcher.UIThread.Post(() =>
                    {
                        notificationService.Show(
                            "Download Completed",
                            $"Download of {e.FileName} completed successfully.",
                            NotificationType.Success
                        );
                    });
                    break;
                case ProgressState.Failed:
                    var msg = "";
                    if (download?.Exception is { } exception)
                    {
                        msg = $"({exception.GetType().Name}) {exception.Message}";
                    }
                    Dispatcher.UIThread.Post(() =>
                    {
                        notificationService.ShowPersistent(
                            "Download Failed",
                            $"Download of {e.FileName} failed: {msg}",
                            NotificationType.Error
                        );
                    });
                    break;
                case ProgressState.Cancelled:
                    Dispatcher.UIThread.Post(() =>
                    {
                        notificationService.Show(
                            "Download Cancelled",
                            $"Download of {e.FileName} was cancelled.",
                            NotificationType.Warning
                        );
                    });
                    break;
            }
        };

        ProgressItems.Add(vm);
    }

    public void AddDownloads(IEnumerable<TrackedDownload> downloads)
    {
        foreach (var download in downloads)
        {
            if (ProgressItems.Any(vm => vm.Id == download.Id))
            {
                continue;
            }
            var vm = new DownloadProgressItemViewModel(download);
            ProgressItems.Add(vm);
        }
    }

    private async Task AddPackageInstall(IPackageModificationRunner packageModificationRunner)
    {
        if (ProgressItems.Any(vm => vm.Id == packageModificationRunner.Id))
        {
            return;
        }

        var vm = new PackageInstallProgressItemViewModel(packageModificationRunner);
        ProgressItems.Add(vm);
        if (packageModificationRunner.ShowDialogOnStart)
        {
            await vm.ShowProgressDialog();
        }
    }

    private void ShowFailedNotification(string title, string message)
    {
        notificationService.ShowPersistent(title, message, NotificationType.Error);
    }

    public void StartEventListener()
    {
        EventManager.Instance.ProgressChanged += OnProgressChanged;
    }

    public void ClearDownloads()
    {
        ProgressItems.RemoveAll(ProgressItems.Where(x => x.IsCompleted));
    }

    private void OnProgressChanged(object? sender, ProgressItem e)
    {
        if (ProgressItems.Any(x => x.Id == e.ProgressId))
            return;

        ProgressItems.Add(new ProgressItemViewModel(e));
    }
}
