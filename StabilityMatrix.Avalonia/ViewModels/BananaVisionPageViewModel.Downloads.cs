using AsyncAwaitBestPractices;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.ViewModels;

public partial class BananaVisionPageViewModel
{
    /// <summary>
    /// Whether there are missing models that can be downloaded
    /// </summary>
    [ObservableProperty]
    public partial bool HasMissingModels { get; set; }

    /// <summary>
    /// Check for missing models and auto-show the download dialog if needed
    /// </summary>
    private async Task CheckAndShowMissingModelsDialogAsync()
    {
        // Don't show if we've already shown it this session
        if (hasShownMissingModelsDialog)
            return;

        // Wait a moment for connection status to settle
        await Task.Delay(500);

        // Only show if connected and models are missing
        if (!ClientManager.IsConnected || !HasMissingModels)
            return;

        hasShownMissingModelsDialog = true;
        await ShowMissingModelsDialogAsync();
    }

    /// <summary>
    /// Show the missing models download dialog
    /// </summary>
    [RelayCommand]
    private async Task ShowMissingModelsDialogAsync()
    {
        if (!ClientManager.IsConnected)
        {
            notificationService.Show(
                "Not Connected",
                "Please connect to ComfyUI first to check for missing models.",
                NotificationType.Warning
            );
            return;
        }

        // Get the model manager for the current provider
        var modelManager = LocalProviderModelManagerRegistry.GetManager(SelectedProviderId);
        if (modelManager == null)
        {
            logger.LogWarning("No model manager found for provider {ProviderId}", SelectedProviderId);
            return;
        }

        var missingModels = modelManager.GetMissingModels(ClientManager).ToList();

        if (missingModels.Count == 0)
        {
            notificationService.Show(
                "All Models Present",
                "All required models are already installed!",
                NotificationType.Success
            );
            return;
        }

        logger.LogInformation(
            "Showing missing models dialog for {Provider} with {Count} models",
            modelManager.ProviderDisplayName,
            missingModels.Count
        );

        // Create and configure the dialog using manager's properties
        var dialogVm = vmFactory.Get<DownloadMissingModelsViewModel>();
        dialogVm.DialogTitle = $"{modelManager.ProviderDisplayName} Setup";
        dialogVm.Description = modelManager.DownloadDialogDescription;
        dialogVm.SetModels(missingModels);

        var dialog = dialogVm.GetDialog();
        var result = await dialog.ShowAsync();

        // If user clicked Download, start the downloads
        if (result == ContentDialogResult.Primary && dialogVm.SelectedCount > 0)
        {
            // Start downloads (runs in background via TrackedDownloadService)
            var downloads = await dialogVm.StartDownloadsAsync();

            if (downloads.Count > 0)
            {
                notificationService.Show(
                    "Downloads Started",
                    $"Downloading {downloads.Count} model(s). Check the progress panel for status.",
                    NotificationType.Information
                );

                // Track completion of all downloads
                TrackDownloadCompletionAsync(downloads, modelManager.ProviderDisplayName)
                    .SafeFireAndForget(ex =>
                    {
                        logger.LogError(ex, "Failed to track download completion");
                    });
            }
        }
    }

    /// <summary>
    /// Track when all downloads complete and show notification
    /// </summary>
    private async Task TrackDownloadCompletionAsync(
        List<TrackedDownload> downloads,
        string providerDisplayName
    )
    {
        var completionTasks = downloads
            .Select(d =>
            {
                var tcs = new TaskCompletionSource<bool>();

                d.ProgressStateChanged += (s, state) =>
                {
                    if (state is ProgressState.Success or ProgressState.Failed or ProgressState.Cancelled)
                    {
                        tcs.TrySetResult(state == ProgressState.Success);
                    }
                };

                // Check if already completed
                if (
                    d.ProgressState
                    is ProgressState.Success
                        or ProgressState.Failed
                        or ProgressState.Cancelled
                )
                {
                    tcs.TrySetResult(d.ProgressState == ProgressState.Success);
                }

                return tcs.Task;
            })
            .ToList();

        // Wait for all downloads to complete
        var results = await Task.WhenAll(completionTasks);
        var successCount = results.Count(r => r);
        var failCount = results.Count(r => !r);

        logger.LogInformation(
            "Model downloads completed: {Success} succeeded, {Failed} failed",
            successCount,
            failCount
        );

        // Refresh model index
        await modelIndexService.RefreshIndex();

        // Reconnect to ComfyUI to refresh model lists
        if (ClientManager.IsConnected)
        {
            try
            {
                await ClientManager.ConnectAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to reconnect after model download");
            }
        }

        // Update status on UI thread
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            UpdateProviderStatus();
            LoadAvailableFluxModels();
            LoadAvailableQwenModels();
        });

        // Show completion notification
        if (failCount == 0 && successCount > 0)
        {
            notificationService.Show(
                "Models Ready! 🎉",
                $"All required models have been downloaded. {providerDisplayName} is ready to use!",
                NotificationType.Success,
                TimeSpan.FromSeconds(8)
            );
        }
        else if (successCount > 0)
        {
            notificationService.Show(
                "Downloads Partially Complete",
                $"{successCount} model(s) downloaded, {failCount} failed. Check the progress panel for details.",
                NotificationType.Warning
            );
        }
        else
        {
            notificationService.Show(
                "Downloads Failed",
                "All model downloads failed. Please check your connection and try again.",
                NotificationType.Error
            );
        }
    }
}
