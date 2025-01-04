using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(SafetensorMetadataDialog))]
[ManagedService]
[RegisterSingleton<SafetensorMetadataViewModel>]
public partial class SafetensorMetadataViewModel : ContentDialogViewModelBase
{
    [ObservableProperty]
    private string? modelName;

    [ObservableProperty]
    private SafetensorMetadata metadata;

    [RelayCommand]
    public void CopyTagToClipboard(string tag)
    {
        App.Clipboard?.SetTextAsync(tag);
    }
}
