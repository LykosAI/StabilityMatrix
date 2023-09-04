using Microsoft.Extensions.Logging;

namespace StabilityMatrix.Avalonia.Diagnostics.LogViewer.Core.Extensions;

public static class LoggerExtensions
{
    public static void Emit(
        this ILogger logger,
        EventId eventId,
        LogLevel logLevel,
        string message,
        Exception? exception = null,
        params object?[] args
    )
    {
        if (logger is null)
            return;

        //if (!logger.IsEnabled(logLevel))
        //    return;

        switch (logLevel)
        {
            case LogLevel.Trace:
                logger.LogTrace(eventId, message, args);
                break;

            case LogLevel.Debug:
                logger.LogDebug(eventId, message, args);
                break;

            case LogLevel.Information:
                logger.LogInformation(eventId, message, args);
                break;

            case LogLevel.Warning:
                logger.LogWarning(eventId, exception, message, args);
                break;

            case LogLevel.Error:
                logger.LogError(eventId, exception, message, args);
                break;

            case LogLevel.Critical:
                logger.LogCritical(eventId, exception, message, args);
                break;
        }
    }

    public static void TestPattern(this ILogger logger, EventId eventId)
    {
        var exception = new Exception("Test Error Message");

        logger.Emit(eventId, LogLevel.Trace, "Trace Test Pattern");
        logger.Emit(eventId, LogLevel.Debug, "Debug Test Pattern");
        logger.Emit(eventId, LogLevel.Information, "Information Test Pattern");
        logger.Emit(eventId, LogLevel.Warning, "Warning Test Pattern");
        logger.Emit(eventId, LogLevel.Error, "Error Test Pattern", exception);
        logger.Emit(eventId, LogLevel.Critical, "Critical Test Pattern", exception);
    }
}
