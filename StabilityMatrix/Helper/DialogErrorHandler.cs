using System.Threading.Tasks;
using System;
using DotNext;
using Microsoft.Extensions.Logging;
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
    private readonly IContentDialogService contentDialogService;
    private readonly ISnackbarService snackbarService;
    private readonly SnackbarViewModel snackbarViewModel;
    
    public DialogErrorHandler(IContentDialogService contentDialogService, ISnackbarService snackbarService, SnackbarViewModel snackbarViewModel)
    {
        this.contentDialogService = contentDialogService;
        this.snackbarService = snackbarService;
        this.snackbarViewModel = snackbarViewModel;
    }
    
    /// <summary>
    /// Shows a generic error snackbar with the given message.
    /// </summary>
    public Task ShowSnackbarAsync(string message, LogLevel level = LogLevel.Error, int timeoutMilliseconds = 5000)
    {
        snackbarViewModel.SnackbarAppearance = level switch
        {
            LogLevel.Error => ControlAppearance.Danger,
            LogLevel.Warning => ControlAppearance.Caution,
            LogLevel.Information => ControlAppearance.Info,
            _ => ControlAppearance.Secondary
        };
        // snackbarService.Timeout = timeoutMilliseconds;
        var icon = new SymbolIcon(SymbolRegular.ErrorCircle24);
        return snackbarService.ShowAsync("Error", message, icon, snackbarViewModel.SnackbarAppearance);
    }
    
    /// <summary>
    /// Attempt to run the given action, showing a generic error snackbar if it fails.
    /// </summary>
    public async Task<Result<T>> TryAsync<T>(Task<T> task, string message, LogLevel level = LogLevel.Error, int timeoutMilliseconds = 5000)
    {
        try
        {
            return await task;
        }
        catch (Exception e)
        {
            await ShowSnackbarAsync(message, level, timeoutMilliseconds);
            return Result.FromException<T>(e);
        }
    }
}
