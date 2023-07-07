using System.Threading.Tasks;
using System;
using AsyncAwaitBestPractices;
using StabilityMatrix.Core.Models;
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
    public TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(5);

    public SnackbarService(Wpf.Ui.Contracts.ISnackbarService snackbarService, SnackbarViewModel snackbarViewModel)
    {
        this.snackbarService = snackbarService;
        this.snackbarViewModel = snackbarViewModel;
    }
    
    /// <inheritdoc />
    public async Task ShowSnackbarAsync(
        string title, 
        string message,
        ControlAppearance appearance = ControlAppearance.Danger, 
        SymbolRegular icon = SymbolRegular.ErrorCircle24,
        TimeSpan? timeout = null)
    {
        snackbarService.Timeout = (int) (timeout ?? DefaultTimeout).TotalMilliseconds;
        await snackbarService.ShowAsync(title, message, new SymbolIcon(icon), appearance);
    }
    
    /// <inheritdoc />
    public async Task<TaskResult<T>> TryAsync<T>(
        Task<T> task,
        string title = "Error",
        string? message = null,
        ControlAppearance appearance = ControlAppearance.Danger,
        SymbolRegular icon = SymbolRegular.ErrorCircle24,
        TimeSpan? timeout = null)
    {
        try
        {
            return new TaskResult<T>(await task);
        }
        catch (Exception e)
        {
            ShowSnackbarAsync(title, message ?? e.Message, appearance, icon, timeout).SafeFireAndForget();
            return TaskResult<T>.FromException(e);
        }
    }
    
    /// <inheritdoc />
    public async Task<TaskResult<bool>> TryAsync(
        Task task,
        string title = "Error",
        string? message = null,
        ControlAppearance appearance = ControlAppearance.Danger,
        SymbolRegular icon = SymbolRegular.ErrorCircle24,
        TimeSpan? timeout = null)
    {
        try
        {
            await task;
            return new TaskResult<bool>(true);
        }
        catch (Exception e)
        {
            ShowSnackbarAsync(title, message ?? e.Message, appearance, icon, timeout).SafeFireAndForget();
            return new TaskResult<bool>(false, e);
        }
    }
}
