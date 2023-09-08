using System.Drawing;
using Microsoft.Extensions.Logging;

namespace StabilityMatrix.Avalonia.Diagnostics.LogViewer.Core.Logging;

public class DataStoreLoggerConfiguration
{
    #region Properties

    public EventId EventId { get; set; }

    public Dictionary<LogLevel, LogEntryColor> Colors { get; } =
        new()
        {
            [LogLevel.Trace] = new LogEntryColor { Foreground = Color.DarkGray },
            [LogLevel.Debug] = new LogEntryColor { Foreground = Color.Gray },
            [LogLevel.Information] = new LogEntryColor { Foreground = Color.WhiteSmoke, },
            [LogLevel.Warning] = new LogEntryColor { Foreground = Color.Orange },
            [LogLevel.Error] = new LogEntryColor
            {
                Foreground = Color.White,
                Background = Color.OrangeRed
            },
            [LogLevel.Critical] = new LogEntryColor
            {
                Foreground = Color.White,
                Background = Color.Red
            },
            [LogLevel.None] = new LogEntryColor { Foreground = Color.Magenta }
        };

    #endregion
}
