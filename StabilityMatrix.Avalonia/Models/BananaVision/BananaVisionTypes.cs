using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Models.BananaVision;

/// <summary>
/// Represents an image pending to be sent with the next message
/// </summary>
public class PendingImage : IDisposable
{
    public required string FilePath { get; init; }
    public required Bitmap Bitmap { get; init; }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;

        Bitmap.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents a provider for display in the ComboBox
/// </summary>
public record ProviderDisplayItem(string Id, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// Represents a selected LoRA with weight settings
/// </summary>
public partial class SelectedLora : ObservableObject
{
    public required HybridModelFile Model { get; init; }

    [ObservableProperty]
    private decimal modelWeight = 1.0m;

    [ObservableProperty]
    private decimal clipWeight = 1.0m;

    public string DisplayName => Model.Local?.DisplayModelName ?? Model.ShortDisplayName;
}

/// <summary>
/// Represents an aspect ratio option for image generation
/// </summary>
public record AspectRatioOption(string Ratio, string Description, int Width, int Height)
{
    public string DisplayName => $"{Ratio} - {Description} ({Width}x{Height})";

    public override string ToString() => DisplayName;
}
