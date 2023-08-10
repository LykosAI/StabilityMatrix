using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ModelCard))]
public partial class ModelCardViewModel : LoadableViewModelBase
{
    [ObservableProperty] private string? selectedModelName;

    public ModelCardViewModel(IInferenceClientManager clientManager)
    {
        ClientManager = clientManager;
    }
    
    public IInferenceClientManager ClientManager { get; }
    
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = DeserializeModel<ModelCardModel>(state);
        SelectedModelName = model.SelectedModelName;
    }

    public override JsonObject SaveStateToJsonObject()
    {
        return SerializeModel(new ModelCardModel(SelectedModelName));
    }
}
