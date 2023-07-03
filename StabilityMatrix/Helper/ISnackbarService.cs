using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Models;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;

namespace StabilityMatrix.Helper;

public interface ISnackbarService
{
    /// <summary>
    /// Shows a generic error snackbar with the given message.
    /// </summary>
    public Task ShowSnackbarAsync(
        string message, 
        string title = "Error", 
        ControlAppearance appearance = ControlAppearance.Danger, 
        SymbolRegular icon = SymbolRegular.ErrorCircle24,
        int timeoutMilliseconds = 5000);

    /// <summary>
    /// Attempt to run the given task, showing a generic error snackbar if it fails.
    /// </summary>
    public Task<TaskResult<T>> TryAsync<T>(
        Task<T> task,
        string message,
        ControlAppearance appearance = ControlAppearance.Danger,
        SymbolRegular icon = SymbolRegular.ErrorCircle24,
        int timeoutMilliseconds = 5000);

    /// <summary>
    /// Attempt to run the given void task, showing a generic error snackbar if it fails.
    /// Return a TaskResult with true if the task succeeded, false if it failed.
    /// </summary>
    Task<TaskResult<bool>> TryAsync(
        Task task,
        string message,
        ControlAppearance appearance = ControlAppearance.Danger,
        SymbolRegular icon = SymbolRegular.ErrorCircle24,
        int timeoutMilliseconds = 5000);
}
