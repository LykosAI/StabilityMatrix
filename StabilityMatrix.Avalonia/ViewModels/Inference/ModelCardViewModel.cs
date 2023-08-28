using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ModelCard))]
public partial class ModelCardViewModel : LoadableViewModelBase
{
    [ObservableProperty] 
    private string? selectedModelName;
    
    [ObservableProperty]
    private string? selectedVaeName;
    
    [ObservableProperty]
    private bool isVaeSelectionEnabled;
    
    public IInferenceClientManager ClientManager { get; }
    
    public ModelCardViewModel(IInferenceClientManager clientManager)
    {
        ClientManager = clientManager;
    }
}
