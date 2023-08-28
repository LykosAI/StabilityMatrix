using Microsoft.Extensions.Logging;

namespace StabilityMatrix.Avalonia.Diagnostics.LogViewer.Core.Logging;

public class LogModel
{
    #region Properties

    public DateTime Timestamp { get; set; }

    public LogLevel LogLevel { get; set; }

    public EventId EventId { get; set; }

    public object? State { get; set; }
    
    public string? LoggerName { get; set; }
    
    public string? CallerClassName { get; set; }
    
    public string? CallerMemberName { get; set; }

    public string? Exception { get; set; }

    public LogEntryColor? Color { get; set; }

    #endregion

    public string LoggerDisplayName => 
        LoggerName?
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault() ?? "";
}
