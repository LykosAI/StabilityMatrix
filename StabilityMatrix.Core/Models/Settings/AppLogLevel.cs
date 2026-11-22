using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Settings;

/// <summary>
/// Minimum level of messages written to the app log file and console.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AppLogLevel>))]
public enum AppLogLevel
{
    Trace,
    Debug,
    Info,
    Warn,
    Error,
}
