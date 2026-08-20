using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAvalonia.UI.Controls;
using NLog;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Avalonia.Helpers;

/// <summary>
/// Shared connect-or-launch flow for features that need a connected ComfyUI client:
/// shows the connection help dialog and, if the user launches a package from it, waits for the
/// connection to be established. Used by the interactive generate path and the prompt queue.
/// </summary>
public static class InferenceConnectionHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Shows the connection help dialog if not connected, then waits for the connection when the
    /// user chose to launch ComfyUI from it. Returns whether the client ended up connected.
    /// </summary>
    /// <param name="progress">Optional progress to show "waiting for startup" state on.</param>
    public static async Task<bool> PromptAndWaitForConnectionAsync(
        IServiceManager<ViewModelBase> vmFactory,
        IInferenceClientManager clientManager,
        RunningPackageService runningPackageService,
        ProgressViewModel? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        if (clientManager.IsConnected)
            return true;

        var vm = vmFactory.Get<InferenceConnectionHelpViewModel>();
        var result = await vm.CreateDialog().ShowAsync();

        if (clientManager.IsConnected)
            return true;

        // If the user chose to launch ComfyUI, the package is now starting up. The connection
        // is established automatically by InferenceViewModel once startup completes, so wait for
        // it here and let the caller resume instead of forcing another button press.
        if (result == ContentDialogResult.Primary && vm.IsLaunchMode)
        {
            return await WaitForLaunchAndConnectionAsync(
                vm.LaunchSelectedPackageCommand.ExecutionTask,
                clientManager,
                runningPackageService,
                progress,
                cancellationToken
            );
        }

        return clientManager.IsConnected;
    }

    /// <summary>
    /// Waits for a package launch to finish, then for the client to connect.
    /// </summary>
    /// <param name="launchTask">
    /// The in-flight launch, or null if nothing was launched. The dialog runs its launch command
    /// through <see cref="System.Windows.Input.ICommand.Execute"/>, which cannot be awaited, so
    /// the package has not registered as running yet when the dialog closes - checking the
    /// connection first would see nothing starting up and give up immediately.
    /// </param>
    public static async Task<bool> WaitForLaunchAndConnectionAsync(
        Task? launchTask,
        IInferenceClientManager clientManager,
        RunningPackageService runningPackageService,
        ProgressViewModel? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        if (launchTask is { IsCompleted: false })
        {
            if (progress is not null)
            {
                progress.IsIndeterminate = true;
                progress.Text = "Waiting for ComfyUI to start...";
            }

            try
            {
                await launchTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                progress?.ClearProgress();
                return clientManager.IsConnected;
            }
            catch (Exception e)
            {
                // The launch surfaces its own errors; fall through so the connection state decides
                Logger.Warn(e, "Package launch faulted while waiting to connect");
            }
        }

        return await WaitForConnectedAsync(clientManager, runningPackageService, progress, cancellationToken);
    }

    /// <summary>
    /// Waits for the client manager to become connected, optionally showing indeterminate
    /// progress. Stops waiting early if ComfyUI is shut down or crashes before connecting
    /// (it is removed from RunningPackages either way).
    /// </summary>
    public static async Task<bool> WaitForConnectedAsync(
        IInferenceClientManager clientManager,
        RunningPackageService runningPackageService,
        ProgressViewModel? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        if (clientManager.IsConnected)
            return true;

        // RunContinuationsAsynchronously so the await resumption (and UI updates in finally)
        // don't run synchronously on the thread that raised the completing event.
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        bool IsAnyComfyRunning() =>
            runningPackageService.RunningPackages.Values.Any(vm => vm.RunningPackage.BasePackage is ComfyUI);

        void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            // null/empty PropertyName means "all properties changed" per INotifyPropertyChanged
            if (
                args.PropertyName is nameof(clientManager.IsConnected) or null or ""
                && clientManager.IsConnected
            )
            {
                tcs.TrySetResult();
            }
        }

        void OnRunningPackagesChanged(object? sender, NotifyCollectionChangedEventArgs args)
        {
            // ComfyUI was shut down or crashed before connecting - stop waiting
            if (!IsAnyComfyRunning())
            {
                tcs.TrySetResult();
            }
        }

        clientManager.PropertyChanged += OnPropertyChanged;
        runningPackageService.RunningPackages.CollectionChanged += OnRunningPackagesChanged;
        try
        {
            // Re-check in case it connected, or the package already stopped, between the
            // initial checks and subscribing
            if (clientManager.IsConnected)
                return true;
            if (!IsAnyComfyRunning())
                return false;

            // Give up waiting after a generous timeout in case startup never completes
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

            if (progress is not null)
            {
                progress.IsIndeterminate = true;
                progress.Text = "Waiting for ComfyUI to start...";
            }

            await using (timeoutCts.Token.Register(() => tcs.TrySetCanceled(timeoutCts.Token)))
            {
                await tcs.Task;
            }

            return clientManager.IsConnected;
        }
        catch (OperationCanceledException)
        {
            return clientManager.IsConnected;
        }
        finally
        {
            clientManager.PropertyChanged -= OnPropertyChanged;
            runningPackageService.RunningPackages.CollectionChanged -= OnRunningPackagesChanged;
            progress?.ClearProgress();
        }
    }
}
