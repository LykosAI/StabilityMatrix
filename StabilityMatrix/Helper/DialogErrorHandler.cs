using System.Threading.Tasks;
using System;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Models;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Common;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls;
using Wpf.Ui.Controls.IconElements;

namespace StabilityMatrix.Helper;

/// <summary>
/// Generic recoverable error handler using content dialogs.
/// </summary>
public class DialogErrorHandler : IDialogErrorHandler
{
    private readonly ISnackbarService snackbarService;
    private readonly SnackbarViewModel snackbarViewModel;

    public DialogErrorHandler(ISnackbarService snackbarService, SnackbarViewModel snackbarViewModel)
    {
        this.snackbarService = snackbarService;
        this.snackbarViewModel = snackbarViewModel;
    }

    /// <summary>
    /// Shows a generic error snackbar with the given message.
    /// </summary>
    public void ShowSnackbarAsync(string message, LogLevel level = LogLevel.Error, int timeoutMilliseconds = 5000)
    {
        snackbarViewModel.SnackbarAppearance = level switch
        {
            LogLevel.Error => ControlAppearance.Danger,
            LogLevel.Warning => ControlAppearance.Caution,
            LogLevel.Information => ControlAppearance.Info,
            _ => ControlAppearance.Secondary
        };
        snackbarService.Timeout = timeoutMilliseconds;
        var icon = new SymbolIcon(SymbolRegular.ErrorCircle24);
        snackbarService.ShowAsync("Error", message, icon, snackbarViewModel.SnackbarAppearance);
    }
    
    /// <summary>
    /// Attempt to run the given action, showing a generic error snackbar if it fails.
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
            ShowSnackbarAsync(message, level, timeoutMilliseconds);
            return new TaskResult<T>
            {
                Exception = e
            };
        }
    }
}
