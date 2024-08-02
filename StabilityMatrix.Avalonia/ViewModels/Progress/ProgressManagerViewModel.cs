using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Collections;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media.Animation;
using FluentIcons.Common;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Settings;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;
using Notification = DesktopNotifications.Notification;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels.Progress;

[View(typeof(ProgressManagerPage))]
[ManagedService]
[Singleton]
public partial class ProgressManagerViewModel : PageViewModelBase
{
    private readonly INotificationService notificationService;
    private readonly INavigationService<MainWindowViewModel> navigationService;
    private readonly INavigationService<SettingsViewModel> settingsNavService;

    public override string Title => "Download Manager";
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.ArrowCircleDown, IconVariant = IconVariant.Filled };
    public AvaloniaList<ProgressItemViewModelBase> ProgressItems { get; } = new();

    [ObservableProperty]
    private bool isOpen;

    public ProgressManagerViewModel(
        ITrackedDownloadService trackedDownloadService,
        INotificationService notificationService,
        INavigationService<MainWindowViewModel> navigationService,
        INavigationService<SettingsViewModel> settingsNavService
    )
    {
        this.notificationService = notificationService;
        this.navigationService = navigationService;
        this.settingsNavService = settingsNavService;

        // Attach to the event
        trackedDownloadService.DownloadAdded += TrackedDownloadService_OnDownloadAdded;
        EventManager.Instance.ToggleProgressFlyout += (_, _) => IsOpen = !IsOpen;
        EventManager.Instance.PackageInstallProgressAdded += InstanceOnPackageInstallProgressAdded;
        EventManager.Instance.RecommendedModelsDialogClosed += InstanceOnRecommendedModelsDialogClosed;
    }

    private void InstanceOnRecommendedModelsDialogClosed(object? sender, EventArgs e)
    {
        var vm = ProgressItems.OfType<PackageInstallProgressItemViewModel>().FirstOrDefault();
        vm?.ShowProgressDialog().SafeFireAndForget();
    }

    private void InstanceOnPackageInstallProgressAdded(object? sender, IPackageModificationRunner runner)
    {
        AddPackageInstall(runner).SafeFireAndForget();
    }

    private void TrackedDownloadService_OnDownloadAdded(object? sender, TrackedDownload e)
    {
        // Attach notification handlers
        // Use Changing because Changed might be called after the download is removed
        e.ProgressStateChanged += (s, state) =>
        {
            Debug.WriteLine($"Download {e.FileName} state changed to {state}");
            var download = s as TrackedDownload;

            switch (state)
            {
                case ProgressState.Success:
                    var imageFile = e.DownloadDirectory.EnumerateFiles(
                        $"{Path.GetFileNameWithoutExtension(e.FileName)}.preview.*"
                    )
                        .FirstOrDefault();

                    notificationService
                        .ShowAsync(
                            NotificationKey.Download_Completed,
                            new Notification
                            {
                                Title = "Download Completed",
                                Body = $"Download of {e.FileName} completed successfully.",
                                BodyImagePath = imageFile?.FullPath
                            }
                        )
                        .SafeFireAndForget();

                    break;
                case ProgressState.Failed:
                    var msg = "";
                    if (download?.Exception is { } exception)
                    {
                        if (
                            exception is UnauthorizedAccessException
                            || exception.InnerException is UnauthorizedAccessException
                        )
                        {
                            Dispatcher.UIThread.InvokeAsync(async () =>
                            {
                                var errorDialog = new BetterContentDialog
                                {
                                    Title = Resources.Label_DownloadFailed,
                                    Content = Resources.Label_CivitAiLoginRequired,
                                    PrimaryButtonText = "Go to Settings",
                                    SecondaryButtonText = "Close",
                                    DefaultButton = ContentDialogButton.Primary,
                                };

                                var result = await errorDialog.ShowAsync();
                                if (result == ContentDialogResult.Primary)
                                {
                                    navigationService.NavigateTo<SettingsViewModel>(
                                        new SuppressNavigationTransitionInfo()
                                    );
                                    await Task.Delay(100);
                                    settingsNavService.NavigateTo<AccountSettingsViewModel>(
                                        new SuppressNavigationTransitionInfo()
                                    );
                                }
                            });

                            return;
                        }

                        msg =
                            $"({exception.GetType().Name}) {exception.InnerException?.Message ?? exception.Message}";
                    }

                    notificationService
                        .ShowPersistentAsync(
                            NotificationKey.Download_Failed,
                            new Notification
                            {
                                Title = "Download Failed",
                                Body = $"Download of {e.FileName} failed: {msg}"
                            }
                        )
                        .SafeFireAndForget();
                    break;
                case ProgressState.Cancelled:
                    notificationService
                        .ShowAsync(
                            NotificationKey.Download_Canceled,
                            new Notification
                            {
                                Title = "Download Cancelled",
                                Body = $"Download of {e.FileName} was cancelled."
                            }
                        )
                        .SafeFireAndForget();
                    break;
            }
        };

        var vm = new DownloadProgressItemViewModel(e);

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

    private Task AddPackageInstall(IPackageModificationRunner packageModificationRunner)
    {
        if (ProgressItems.Any(vm => vm.Id == packageModificationRunner.Id))
        {
            return Task.CompletedTask;
        }

        var vm = new PackageInstallProgressItemViewModel(packageModificationRunner);
        ProgressItems.Add(vm);

        return packageModificationRunner.ShowDialogOnStart ? vm.ShowProgressDialog() : Task.CompletedTask;
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
