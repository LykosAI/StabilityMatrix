using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using StabilityMatrix.Avalonia.Diagnostics.LogViewer.Core.Logging;
using StabilityMatrix.Avalonia.Diagnostics.LogViewer.Core.ViewModels;
using LogDataStore = StabilityMatrix.Avalonia.Diagnostics.LogViewer.Logging.LogDataStore;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace StabilityMatrix.Avalonia.Diagnostics.LogViewer.Extensions;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class ServicesExtension
{
    public static IServiceCollection AddLogViewer(this IServiceCollection services)
    {
        services.AddSingleton<ILogDataStore>(Core.Logging.LogDataStore.Instance);
        services.AddSingleton<LogViewerControlViewModel>();

        return services;
    }

    public static IServiceCollection AddLogViewer(
        this IServiceCollection services,
        Action<DataStoreLoggerConfiguration> configure
    )
    {
        services.AddSingleton<ILogDataStore>(Core.Logging.LogDataStore.Instance);
        services.AddSingleton<LogViewerControlViewModel>();
        services.Configure(configure);

        return services;
    }

    public static ILoggingBuilder AddNLogTargets(
        this ILoggingBuilder builder,
        IConfiguration config
    )
    {
        LogManager
            .Setup()
            // Register custom Target
            .SetupExtensions(
                extensionBuilder =>
                    extensionBuilder.RegisterTarget<DataStoreLoggerTarget>("DataStoreLogger")
            );

        /*builder
            .ClearProviders()
            .SetMinimumLevel(MsLogLevel.Trace)
            // Load NLog settings from appsettings*.json
            .AddNLog(config,
                // custom options for capturing the EventId information
                new NLogProviderOptions
                {
                    // https://nlog-project.org/2021/08/25/nlog-5-0-preview1-ready.html#nlogextensionslogging-changes-capture-of-eventid
                    IgnoreEmptyEventId = false,
                    CaptureEventId = EventIdCaptureType.Legacy
                });*/

        return builder;
    }

    public static ILoggingBuilder AddNLogTargets(
        this ILoggingBuilder builder,
        IConfiguration config,
        Action<DataStoreLoggerConfiguration> configure
    )
    {
        builder.AddNLogTargets(config);
        builder.Services.Configure(configure);
        return builder;
    }
}
