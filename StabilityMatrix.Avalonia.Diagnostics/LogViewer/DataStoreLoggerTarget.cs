using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Targets;
using StabilityMatrix.Avalonia.Diagnostics.LogViewer.Core.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace StabilityMatrix.Avalonia.Diagnostics.LogViewer;

[Target("DataStoreLogger")]
public class DataStoreLoggerTarget : TargetWithLayout
{
    #region Fields

    private ILogDataStore? _dataStore;
    private DataStoreLoggerConfiguration? _config;

    #endregion

    #region methods

    protected override void InitializeTarget()
    {
        // we need to inject dependencies
        // var serviceProvider = ResolveService<IServiceProvider>();

        // reference the shared instance
        _dataStore = LogDataStore.Instance;
        // _dataStore = serviceProvider.GetRequiredService<ILogDataStore>();

        // load the config options
        /*var options
            = serviceProvider.GetService<IOptionsMonitor<DataStoreLoggerConfiguration>>();*/

        // _config = options?.CurrentValue ?? new DataStoreLoggerConfiguration();
        _config = new DataStoreLoggerConfiguration();

        base.InitializeTarget();
    }

    protected override void Write(LogEventInfo logEvent)
    {
        // cast NLog Loglevel to Microsoft LogLevel type
        var logLevel = (MsLogLevel)Enum.ToObject(typeof(MsLogLevel), logEvent.Level.Ordinal);

        // format the message
        var message = RenderLogEvent(Layout, logEvent);

        // retrieve the EventId
        logEvent.Properties.TryGetValue("EventId", out var result);
        if (result is not EventId eventId)
        {
            eventId = _config!.EventId;
        }

        // add log entry
        _dataStore?.AddEntry(new LogModel
        {
            Timestamp = DateTime.UtcNow,
            LogLevel = logLevel,
            // do we override the default EventId if it exists?
            EventId = eventId.Id == 0 && (_config?.EventId.Id ?? 0) != 0 ? _config!.EventId : eventId,
            State = message,
            LoggerName = logEvent.LoggerName,
            CallerClassName = logEvent.CallerClassName,
            CallerMemberName = logEvent.CallerMemberName,
            Exception = logEvent.Exception?.Message ?? (logLevel == MsLogLevel.Error ? message : ""),
            Color = _config!.Colors[logLevel],
        });
        
        Debug.WriteLine($"--- [{logLevel.ToString()[..3]}] {message} - {logEvent.Exception?.Message ?? "no error"}");
    }

    #endregion
}
