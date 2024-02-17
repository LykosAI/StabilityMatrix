using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ControlNetCard))]
[ManagedService]
[Transient]
public partial class ControlNetCardViewModel : LoadableViewModelBase
{
    public const string ModuleKey = "ControlNet";

    private readonly ITrackedDownloadService trackedDownloadService;
    private readonly ISettingsManager settingsManager;
    private readonly ServiceManager<ViewModelBase> vmFactory;

    [ObservableProperty]
    [Required]
    private HybridModelFile? selectedModel;

    [ObservableProperty]
    [Required]
    private HybridModelFile? selectedPreprocessor;

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
        ITrackedDownloadService trackedDownloadService,
        ISettingsManager settingsManager,
        IInferenceClientManager clientManager,
        ServiceManager<ViewModelBase> vmFactory
    )
    {
        this.trackedDownloadService = trackedDownloadService;
        this.settingsManager = settingsManager;
        this.vmFactory = vmFactory;

        ClientManager = clientManager;
        SelectImageCardViewModel = vmFactory.Get<SelectImageCardViewModel>();
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
