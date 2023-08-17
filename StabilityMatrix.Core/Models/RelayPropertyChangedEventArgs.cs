using System.ComponentModel;

namespace StabilityMatrix.Core.Models;

public class RelayPropertyChangedEventArgs : PropertyChangedEventArgs
{
    public bool IsRelay { get; }

    /// <inheritdoc />
    public RelayPropertyChangedEventArgs(string? propertyName, bool isRelay = false)
        : base(propertyName)
    {
        IsRelay = isRelay;
    }
}
