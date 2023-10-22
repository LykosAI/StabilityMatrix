using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Models;

/// <summary>
/// Singleton DI service for observable shared UI state.
/// </summary>
[Singleton]
public partial class SharedState : ObservableObject
{
    /// <summary>
    /// Whether debug mode enabled from settings page version tap.
    /// </summary>
    [ObservableProperty]
    private bool isDebugMode;
}
