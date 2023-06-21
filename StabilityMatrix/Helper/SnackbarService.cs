using System.Threading.Tasks;
using System;
using AsyncAwaitBestPractices;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Models;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;
using Wpf.Ui.Controls.IconElements;

namespace StabilityMatrix.Helper;

/// <summary>
/// Generic recoverable error handler using content dialogs.
/// </summary>
public class SnackbarService : ISnackbarService
{
    private readonly Wpf.Ui.Contracts.ISnackbarService snackbarService;
    private readonly SnackbarViewModel snackbarViewModel;

    public SnackbarService(Wpf.Ui.Contracts.ISnackbarService snackbarService, SnackbarViewModel snackbarViewModel)
    {
        this.snackbarService = snackbarService;
        this.snackbarViewModel = snackbarViewModel;
    }

    /// <summary>
    /// Shows a generic error snackbar with the given message.
    /// </summary>
    /// <param name="level">
    ///     Controls the appearance of the snackbar.
    ///     Error => Danger
    ///     Warning => Caution
    ///     Information => Info
    ///     Trace => Success
    ///     Other => Secondary
    /// </param>
    public async Task ShowSnackbarAsync(string message, string title = "Error", LogLevel level = LogLevel.Error, int timeoutMilliseconds = 5000)
    {
        snackbarViewModel.SnackbarAppearance = level switch
        {
            LogLevel.Error => ControlAppearance.Danger,
            LogLevel.Warning => ControlAppearance.Caution,
            LogLevel.Information => ControlAppearance.Info,
            LogLevel.Trace => ControlAppearance.Success,
            _ => ControlAppearance.Secondary
        };
        snackbarService.Timeout = timeoutMilliseconds;
        var icon = new SymbolIcon(SymbolRegular.ErrorCircle24);
        await snackbarService.ShowAsync(title, message, icon, snackbarViewModel.SnackbarAppearance);
    }
    
    /// <summary>
    /// Attempt to run the given task, showing a generic error snackbar if it fails.
    /// </summary>
    public async Task<TaskResult<T>> TryAsync<T>(Task<T> task, string message, LogLevel level = LogLevel.Error, int timeoutMilliseconds = 5000)
    {
        try
        {
            return new TaskResult<T>
            {
                Result = await task
            };
        }
        catch (Exception e)
        {
            ShowSnackbarAsync(message, level: level, timeoutMilliseconds: timeoutMilliseconds).SafeFireAndForget();
            return new TaskResult<T>
            {
                Exception = e
            };
        }
    }
    
    /// <summary>
    /// Attempt to run the given void task, showing a generic error snackbar if it fails.
    /// Return a TaskResult with true if the task succeeded, false if it failed.
    /// </summary>
    public async Task<TaskResult<bool>> TryAsync(Task task, string message, LogLevel level = LogLevel.Error, int timeoutMilliseconds = 5000)
    {
        try
        {
            await task;
            return new TaskResult<bool>
            {
                Result = true
            };
        }
        catch (Exception e)
        {
            ShowSnackbarAsync(message, level: level, timeoutMilliseconds: timeoutMilliseconds);
            return new TaskResult<bool>
            {
                Result = false,
                Exception = e
            };
        }
    }
}
