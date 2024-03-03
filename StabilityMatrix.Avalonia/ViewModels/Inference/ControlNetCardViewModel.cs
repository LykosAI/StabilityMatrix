using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ControlNetCard))]
[ManagedService]
[Transient]
public partial class ControlNetCardViewModel : LoadableViewModelBase
{
    public const string ModuleKey = "ControlNet";

    private readonly ServiceManager<ViewModelBase> vmFactory;

    [ObservableProperty]
    [Required]
    private HybridModelFile? selectedModel;

    [ObservableProperty]
    [Required]
    private ComfyAuxPreprocessor? selectedPreprocessor;

    [ObservableProperty]
    [Required]
    [Range(0, 2048)]
    private int width;

    [ObservableProperty]
    [Required]
    [Range(0, 2048)]
    private int height;

    [ObservableProperty]
    [Required]
    [Range(0d, 10d)]
    private double strength = 1.0;

    [ObservableProperty]
    [Required]
    [Range(0d, 1d)]
    private double startPercent;

    [ObservableProperty]
    [Required]
    [Range(0d, 1d)]
    private double endPercent = 1.0;

    public SelectImageCardViewModel SelectImageCardViewModel { get; }

    public IInferenceClientManager ClientManager { get; }

    public ControlNetCardViewModel(
        IInferenceClientManager clientManager,
        ServiceManager<ViewModelBase> vmFactory
    )
    {
        this.vmFactory = vmFactory;

        ClientManager = clientManager;
        SelectImageCardViewModel = vmFactory.Get<SelectImageCardViewModel>();

        // Update our width and height when the image changes
        SelectImageCardViewModel
            .WhenPropertyChanged(card => card.CurrentBitmapSize)
            .Subscribe(propertyValue =>
            {
                if (!propertyValue.Value.IsEmpty)
                {
                    Width = propertyValue.Value.Width;
                    Height = propertyValue.Value.Height;
                }
            });
    }

    [RelayCommand]
    private async Task RemoteDownload(HybridModelFile? modelFile)
    {
        if (modelFile?.DownloadableResource is not { } resource)
            return;

        var confirmDialog = vmFactory.Get<DownloadResourceViewModel>();
        confirmDialog.Resource = resource;
        confirmDialog.FileName = modelFile.FileName;

        if (await confirmDialog.GetDialog().ShowAsync() == ContentDialogResult.Primary)
        {
            confirmDialog.StartDownload();
        }
    }
}
