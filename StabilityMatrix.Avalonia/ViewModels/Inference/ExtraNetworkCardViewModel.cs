using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ExtraNetworkCard))]
[ManagedService]
[RegisterTransient<ExtraNetworkCardViewModel>]
public partial class ExtraNetworkCardViewModel(IInferenceClientManager clientManager) : LoadableViewModelBase
{
    public const string ModuleKey = "ExtraNetwork";

    /// <summary>
    /// Whether user can toggle model weight visibility
    /// </summary>
    [JsonIgnore]
    public bool IsModelWeightToggleEnabled { get; set; }

    /// <summary>
    /// Whether user can toggle clip weight visibility
    /// </summary>
    [JsonIgnore]
    public bool IsClipWeightToggleEnabled { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TriggerWords), nameof(ShowTriggerWords))]
    private HybridModelFile? selectedModel;

    [ObservableProperty]
    private bool isModelWeightEnabled;

    [ObservableProperty]
    [property: Category("Settings")]
    [property: DisplayName("CLIP Strength Adjustment")]
    private bool isClipWeightEnabled;

    [ObservableProperty]
    private double modelWeight = 1.0;

    [ObservableProperty]
    private double clipWeight = 1.0;

    public string TriggerWords =>
        SelectedModel?.Local?.ConnectedModelInfo?.TrainedWordsString ?? string.Empty;
    public bool ShowTriggerWords => !string.IsNullOrWhiteSpace(TriggerWords);

    public IInferenceClientManager ClientManager { get; } = clientManager;

    /// <inheritdoc />
    public override JsonObject SaveStateToJsonObject()
    {
        return SerializeModel(
            new ExtraNetworkCardModel
            {
                SelectedModelName = SelectedModel?.RelativePath,
                IsModelWeightEnabled = IsModelWeightEnabled,
                IsClipWeightEnabled = IsClipWeightEnabled,
                ModelWeight = ModelWeight,
                ClipWeight = ClipWeight
            }
        );
    }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = DeserializeModel<ExtraNetworkCardModel>(state);

        SelectedModel = model.SelectedModelName is null
            ? null
            : ClientManager.LoraModels.FirstOrDefault(x => x.RelativePath == model.SelectedModelName);

        IsModelWeightEnabled = model.IsModelWeightEnabled;
        IsClipWeightEnabled = model.IsClipWeightEnabled;
        ModelWeight = model.ModelWeight;
        ClipWeight = model.ClipWeight;
    }

    [RelayCommand]
    private void CopyTriggerWords()
    {
        if (!ShowTriggerWords)
            return;

        App.Clipboard.SetTextAsync(TriggerWords);
    }

    internal class ExtraNetworkCardModel
    {
        public string? SelectedModelName { get; init; }
        public bool IsModelWeightEnabled { get; init; }
        public bool IsClipWeightEnabled { get; init; }
        public double ModelWeight { get; init; }
        public double ClipWeight { get; init; }
    }
}
