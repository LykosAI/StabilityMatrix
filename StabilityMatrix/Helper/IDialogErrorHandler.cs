using System;
using System.Threading.Tasks;
using DotNext;
using Microsoft.Extensions.Logging;

namespace StabilityMatrix.Helper;

public interface IDialogErrorHandler
{
    /// <summary>
    /// Shows a generic error snackbar with the given message.
    /// </summary>
    Task ShowSnackbarAsync(string message, LogLevel level = LogLevel.Error, int timeoutSeconds = 5);

    /// <summary>
    /// Attempt to run the given action, showing a generic error snackbar if it fails.
    /// </summary>
    Task<Result<T>> TryAsync<T>(Task<T> task, string message, LogLevel level = LogLevel.Error, int timeoutSeconds = 5);
}
