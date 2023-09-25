using System.Linq;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ModelCard))]
public partial class ModelCardViewModel : LoadableViewModelBase
{
    [ObservableProperty]
    private HybridModelFile? selectedModel;

    [ObservableProperty]
    private bool isRefinerSelectionEnabled;

    [ObservableProperty]
    private HybridModelFile? selectedRefiner = HybridModelFile.None;

    [ObservableProperty]
    private HybridModelFile? selectedVae = HybridModelFile.Default;

    [ObservableProperty]
    private bool isVaeSelectionEnabled;

    public string? SelectedModelName => SelectedModel?.FileName;

    public string? SelectedVaeName => SelectedVae?.FileName;

    public IInferenceClientManager ClientManager { get; }

    public ModelCardViewModel(IInferenceClientManager clientManager)
    {
        ClientManager = clientManager;
    }

    /// <inheritdoc />
    public override JsonObject SaveStateToJsonObject()
    {
        return SerializeModel(
            new ModelCardModel
            {
                SelectedModelName = SelectedModelName,
                SelectedVaeName = SelectedVaeName,
                IsVaeSelectionEnabled = IsVaeSelectionEnabled
            }
        );
    }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = DeserializeModel<ModelCardModel>(state);

        SelectedModel = model.SelectedModelName is null
            ? null
            : ClientManager.Models.FirstOrDefault(x => x.FileName == model.SelectedModelName);

        SelectedVae = model.SelectedVaeName is null
            ? HybridModelFile.Default
            : ClientManager.VaeModels.FirstOrDefault(x => x.FileName == model.SelectedVaeName);
    }

    internal class ModelCardModel
    {
        public string? SelectedModelName { get; init; }
        public string? SelectedVaeName { get; init; }
        public bool IsVaeSelectionEnabled { get; init; }
    }
}
