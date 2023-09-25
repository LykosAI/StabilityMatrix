using System;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.Input;
using NLog;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.Extensions;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class RelayCommandExtensions
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static void VerifyFlowExceptionsToTaskSchedulerEnabled(IAsyncRelayCommand command)
    {
        // Check that the FlowExceptionsToTaskScheduler flag is set
        var options = command.GetPrivateField<AsyncRelayCommandOptions>("options");

        if (!options.HasFlag(AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler))
        {
            throw new ArgumentException(
                "The command must be created with the FlowExceptionsToTaskScheduler option enabled"
            );
        }
    }

    /// <summary>
    /// Attach an error handler to the command that will invoke the given action when an exception occurs.
    /// </summary>
    /// <param name="command">The command to attach the error handler to.</param>
    /// <param name="onError">The action to invoke when an exception occurs.</param>
    /// <exception cref="ArgumentException">Thrown if the command was not created with the FlowExceptionsToTaskScheduler option enabled.</exception>
    public static T WithErrorHandler<T>(this T command, Action<Exception> onError)
        where T : IAsyncRelayCommand
    {
        VerifyFlowExceptionsToTaskSchedulerEnabled(command);

        command.PropertyChanged += (sender, e) =>
        {
            if (sender is not IAsyncRelayCommand senderCommand)
            {
                return;
            }
            // On ExecutionTask updates, check if there is an exception
            if (
                e.PropertyName == nameof(AsyncRelayCommand.ExecutionTask)
                && senderCommand.ExecutionTask is { Exception: { } exception }
            )
            {
                onError(exception.InnerException ?? exception);
            }
        };

        return command;
    }

    /// <summary>
    /// Conditionally attach an error handler to the command that will invoke the given action when an exception occurs.
    /// The error is propagated if not in DEBUG mode.
    /// </summary>
    /// <param name="command">The command to attach the error handler to.</param>
    /// <param name="onError">The action to invoke when an exception occurs.</param>
    /// <exception cref="ArgumentException">Thrown if the command was not created with the FlowExceptionsToTaskScheduler option enabled.</exception>
    public static T WithConditionalErrorHandler<T>(this T command, Action<Exception> onError)
        where T : IAsyncRelayCommand
    {
        VerifyFlowExceptionsToTaskSchedulerEnabled(command);

#if DEBUG
        command.PropertyChanged += (sender, e) =>
        {
            if (sender is not IAsyncRelayCommand senderCommand)
            {
                return;
            }
            // On ExecutionTask updates, check if there is an exception
            if (
                e.PropertyName == nameof(AsyncRelayCommand.ExecutionTask)
                && senderCommand.ExecutionTask is { Exception: { } exception }
            )
            {
                if (exception.InnerException != null)
                {
                    throw exception.InnerException;
                }
                throw exception;
            }
        };

        return command;
#else
        return WithErrorHandler(command, onError);
#endif
    }

    /// <summary>
    /// Attach an error handler to the command that will log the error and show a notification.
    /// </summary>
    /// <param name="command">The command to attach the error handler to.</param>
    /// <param name="notificationService">The notification service to use to show the notification.</param>
    /// <param name="logLevel">The log level to use when logging the error. Defaults to LogLevel.Error</param>
    /// <exception cref="ArgumentException">Thrown if the command was not created with the FlowExceptionsToTaskScheduler option enabled.</exception>
    public static T WithNotificationErrorHandler<T>(
        this T command,
        INotificationService notificationService,
        LogLevel? logLevel = default
    )
        where T : IAsyncRelayCommand
    {
        logLevel ??= LogLevel.Error;

        return command.WithErrorHandler(e =>
        {
            Logger.Log(logLevel, e, "Error executing command");
            notificationService.ShowPersistent("Error", $"[{e.GetType().Name}] {e.Message}");
        });
    }

    /// <summary>
    /// Attach an error handler to the command that will log the error and show a notification.
    /// The error is propagated if not in DEBUG mode.
    /// </summary>
    /// <param name="command">The command to attach the error handler to.</param>
    /// <param name="notificationService">The notification service to use to show the notification.</param>
    /// <param name="logLevel">The log level to use when logging the error. Defaults to LogLevel.Error</param>
    /// <exception cref="ArgumentException">Thrown if the command was not created with the FlowExceptionsToTaskScheduler option enabled.</exception>
    public static T WithConditionalNotificationErrorHandler<T>(
        this T command,
        INotificationService notificationService,
        LogLevel? logLevel = default
    )
        where T : IAsyncRelayCommand
    {
        logLevel ??= LogLevel.Error;

        return command.WithConditionalErrorHandler(e =>
        {
            Logger.Log(logLevel, e, "Error executing command");
            notificationService.ShowPersistent("Error", $"[{e.GetType().Name}] {e.Message}");
        });
    }
}
