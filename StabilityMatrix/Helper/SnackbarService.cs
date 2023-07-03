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
    
    /// <inheritdoc />
    public async Task ShowSnackbarAsync(
        string message, 
        string title = "Error", 
        ControlAppearance appearance = ControlAppearance.Danger,
        SymbolRegular icon = SymbolRegular.ErrorCircle24,
        int timeoutMilliseconds = 5000)
    {
        snackbarService.Timeout = timeoutMilliseconds;
        await snackbarService.ShowAsync(title, message, new SymbolIcon(icon), appearance);
    }
    
    /// <inheritdoc />
    public async Task<TaskResult<T>> TryAsync<T>(
        Task<T> task, 
        string message, 
        ControlAppearance appearance = ControlAppearance.Danger, 
        SymbolRegular icon = SymbolRegular.ErrorCircle24,
        int timeoutMilliseconds = 5000)
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
    
    /// <inheritdoc />
    public async Task<TaskResult<bool>> TryAsync(
        Task task, 
        string message, 
        ControlAppearance appearance = ControlAppearance.Danger, 
        SymbolRegular icon = SymbolRegular.ErrorCircle24,
        int timeoutMilliseconds = 5000)
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
