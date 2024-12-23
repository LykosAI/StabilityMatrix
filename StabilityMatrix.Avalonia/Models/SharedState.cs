using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;

namespace StabilityMatrix.Avalonia.Models;

/// <summary>
/// Singleton DI service for observable shared UI state.
/// </summary>
[RegisterSingleton<SharedState>]
public partial class SharedState : ObservableObject
{
    /// <summary>
    /// Whether debug mode enabled from settings page version tap.
    /// </summary>
    [ObservableProperty]
    private bool isDebugMode;
}
