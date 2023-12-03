using System;

namespace StabilityMatrix.Avalonia.Models;

public class TypedNavigationEventArgs : EventArgs
{
    public required Type ViewModelType { get; init; }

    public object? ViewModel { get; init; }
}
