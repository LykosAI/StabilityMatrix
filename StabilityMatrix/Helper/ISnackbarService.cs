using System;
using System.Threading.Tasks;
using StabilityMatrix.Core.Models;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;

namespace StabilityMatrix.Helper;

public interface ISnackbarService
{
    /// <summary>
    /// Default timeout for snackbar messages.
    /// </summary>
    public TimeSpan DefaultTimeout { get; }
    
    /// <summary>
    /// Shows a generic error snackbar with the given message.
    /// </summary>
    /// <param name="title">The title to show in the snackbar.</param>
    /// <param name="message">The message to show</param>
    /// <param name="appearance">The appearance of the snackbar.</param>
    /// <param name="icon">The icon to show in the snackbar.</param>
    /// <param name="timeout">Snackbar timeout, defaults to class DefaultTimeout</param>
    public Task ShowSnackbarAsync(
        string title, 
        string message,
        ControlAppearance appearance = ControlAppearance.Danger, 
        SymbolRegular icon = SymbolRegular.ErrorCircle24,
        TimeSpan? timeout = null);

    /// <summary>
    /// Attempt to run the given task, showing a generic error snackbar if it fails.
    /// </summary>
    /// <param name="task">The task to run.</param>
    /// <param name="title">The title to show in the snackbar.</param>
    /// <param name="message">The message to show, default to exception.Message</param>
    /// <param name="appearance">The appearance of the snackbar.</param>
    /// <param name="icon">The icon to show in the snackbar.</param>
    /// <param name="timeout">Snackbar timeout, defaults to class DefaultTimeout</param>
    public Task<TaskResult<T>> TryAsync<T>(
        Task<T> task,
        string title = "Error",
        string? message = null,
        ControlAppearance appearance = ControlAppearance.Danger,
        SymbolRegular icon = SymbolRegular.ErrorCircle24,
        TimeSpan? timeout = null);

    /// <summary>
    /// Attempt to run the given void task, showing a generic error snackbar if it fails.
    /// Return a TaskResult with true if the task succeeded, false if it failed.
    /// </summary>
    /// <param name="task">The task to run.</param>
    /// <param name="title">The title to show in the snackbar.</param>
    /// <param name="message">The message to show, default to exception.Message</param>
    /// <param name="appearance">The appearance of the snackbar.</param>
    /// <param name="icon">The icon to show in the snackbar.</param>
    /// <param name="timeout">Snackbar timeout, defaults to class DefaultTimeout</param>
    Task<TaskResult<bool>> TryAsync(
        Task task,
        string title = "Error",
        string? message = null,
        ControlAppearance appearance = ControlAppearance.Danger,
        SymbolRegular icon = SymbolRegular.ErrorCircle24,
        TimeSpan? timeout = null);
}
