using System;
using System.Linq;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ModelCard))]
[ManagedService]
[Transient]
public partial class ModelCardViewModel : LoadableViewModelBase, IParametersLoadableState
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

    /// <inheritdoc />
    public void LoadStateFromParameters(GenerationParameters parameters)
    {
        if (parameters.ModelName is not { } paramsModelName)
            return;

        var currentModels = ClientManager.Models;

        HybridModelFile? model;

        // First try hash match
        if (parameters.ModelHash is not null)
        {
            model = currentModels.FirstOrDefault(
                m =>
                    m.Local?.ConnectedModelInfo?.Hashes.SHA256 is { } sha256
                    && sha256.StartsWith(
                        parameters.ModelHash,
                        StringComparison.InvariantCultureIgnoreCase
                    )
            );
        }
        else
        {
            // Name matches
            model = currentModels.FirstOrDefault(m => m.FileName.EndsWith(paramsModelName));
            model ??= currentModels.FirstOrDefault(
                m => m.ShortDisplayName.StartsWith(paramsModelName)
            );
        }

        if (model is not null)
        {
            SelectedModel = model;
        }
    }

    /// <inheritdoc />
    public GenerationParameters SaveStateToParameters(GenerationParameters parameters)
    {
        return parameters with
        {
            ModelName = SelectedModel?.FileName,
            ModelHash = SelectedModel?.Local?.ConnectedModelInfo?.Hashes.SHA256
        };
    }
}
