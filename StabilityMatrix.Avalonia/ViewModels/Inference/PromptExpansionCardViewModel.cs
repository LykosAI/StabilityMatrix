using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(PromptExpansionCard))]
[ManagedService]
[Transient]
public partial class PromptExpansionCardViewModel(
    IInferenceClientManager clientManager,
    ServiceManager<ViewModelBase> vmFactory
) : LoadableViewModelBase
{
    public const string ModuleKey = "PromptExpansion";

    public IInferenceClientManager ClientManager { get; } = clientManager;

    [ObservableProperty]
    private HybridModelFile? selectedModel;

    [ObservableProperty]
    private bool isLogOutputEnabled = true;

    [RelayCommand]
    private async Task RemoteDownload(HybridModelFile? model)
    {
        if (model?.DownloadableResource is not { } resource)
            return;

        var confirmDialog = vmFactory.Get<DownloadResourceViewModel>();
        confirmDialog.Resource = resource;
        confirmDialog.FileName = resource.FileName;

        if (await confirmDialog.GetDialog().ShowAsync() == ContentDialogResult.Primary)
        {
            confirmDialog.StartDownload();
        }
    }
}
