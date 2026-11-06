using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Avalonia.Logging;

/// <summary>
/// Applies the user's <see cref="AppLogLevel"/> to the app's file and console log rules at runtime.
/// </summary>
public static class AppLogLevelApplier
{
    public static LogLevel ToNLogLevel(this AppLogLevel level) =>
        level switch
        {
            AppLogLevel.Trace => LogLevel.Trace,
            AppLogLevel.Debug => LogLevel.Debug,
            AppLogLevel.Info => LogLevel.Info,
            AppLogLevel.Warn => LogLevel.Warn,
            AppLogLevel.Error => LogLevel.Error,
            _ => LogLevel.Debug,
        };

    /// <summary>
    /// Sets the minimum level of the catch-all rules writing to the log file or console in
    /// <paramref name="configuration"/>. Rules for specific loggers (such as the filters that
    /// limit System.* and Microsoft.* to warnings) are left unchanged.
    /// </summary>
    /// <returns>The number of rules updated.</returns>
    public static int Apply(LoggingConfiguration? configuration, AppLogLevel level)
    {
        if (configuration is null)
            return 0;

        var minLevel = level.ToNLogLevel();
        var updated = 0;

        foreach (var rule in configuration.LoggingRules)
        {
            if (rule.LoggerNamePattern != "*")
                continue;

            if (!rule.Targets.Select(Unwrap).Any(t => t is FileTarget or RichConsoleTarget))
                continue;

            rule.SetLoggingLevels(minLevel, LogLevel.Fatal);
            updated++;
        }

        return updated;
    }

    /// <summary>
    /// Applies <paramref name="level"/> to the active NLog configuration and refreshes existing loggers.
    /// </summary>
    public static void ApplyToActiveConfiguration(AppLogLevel level)
    {
        if (Apply(LogManager.Configuration, level) > 0)
        {
            LogManager.ReconfigExistingLoggers();
        }
    }

    private static Target Unwrap(Target target)
    {
        while (target is WrapperTargetBase { WrappedTarget: { } inner })
        {
            target = inner;
        }
        return target;
    }
}
