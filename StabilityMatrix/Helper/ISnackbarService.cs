using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Models;

namespace StabilityMatrix.Helper;

public interface ISnackbarService
{
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
    Task ShowSnackbarAsync(string message, string title = "Error", LogLevel level = LogLevel.Error, int timeoutMilliseconds = 5000);

    /// <summary>
    /// Attempt to run the given task, showing a generic error snackbar if it fails.
    /// </summary>
    Task<TaskResult<T>> TryAsync<T>(Task<T> task, string message, LogLevel level = LogLevel.Error, int timeoutMilliseconds = 5000);

    /// <summary>
    /// Attempt to run the given void task, showing a generic error snackbar if it fails.
    /// Return a TaskResult with true if the task succeeded, false if it failed.
    /// </summary>
    Task<TaskResult<bool>> TryAsync(Task task, string message, LogLevel level = LogLevel.Error, int timeoutMilliseconds = 5000);
}
