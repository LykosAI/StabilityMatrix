using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services;

#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ExtraNetworkCard))]
[ManagedService]
[RegisterTransient<ExtraNetworkCardViewModel>]
public partial class ExtraNetworkCardViewModel : DisposableLoadableViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly ModelCompatChecker modelCompatChecker = new();

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

    [ObservableProperty]
    private HybridModelFile? selectedBaseModel;

    /// <inheritdoc/>
    public ExtraNetworkCardViewModel(IInferenceClientManager clientManager, ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
        ClientManager = clientManager;

        // Observable signal when SelectedBaseModel changes
        var baseModelChangedSignal = this.WhenPropertyChanged(vm => vm.SelectedBaseModel)
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Select(_ => Unit.Default);

        // Observable signal when the FilterExtraNetworksByBaseModel setting changes
        var settingChangedSignal = settingsManager
            .ObservePropertyChanged(s => s.FilterExtraNetworksByBaseModel)
            .Select(_ => Unit.Default);

        // Combine signals
        var reapplyFilterSignal = Observable
            .Merge([baseModelChangedSignal, settingChangedSignal])
            // StartWith ensures the filter is applied at least once initially
            .StartWith(Unit.Default);

        var filterPredicate = reapplyFilterSignal
            .ObserveOn(SynchronizationContext.Current!)
            .Select(_ =>
            {
                if (!settingsManager.Settings.FilterExtraNetworksByBaseModel)
                    return (Func<HybridModelFile, bool>)(_ => true);

                return (Func<HybridModelFile, bool>)FilterCompatibleLoras;
            });

        AddDisposable(
            ClientManager
                .LoraModelsChangeSet.DeferUntilLoaded()
                .Filter(filterPredicate)
                .SortAndBind(
                    LoraModels,
                    SortExpressionComparer<HybridModelFile>
                        .Ascending(f => f.Type)
                        .ThenByAscending(f => f.SortKey)
                )
                .ObserveOn(SynchronizationContext.Current!)
                .Subscribe()
        );
    }

    public IObservableCollection<HybridModelFile> LoraModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    public string TriggerWords =>
        SelectedModel?.Local?.ConnectedModelInfo?.TrainedWordsString ?? string.Empty;
    public bool ShowTriggerWords => !string.IsNullOrWhiteSpace(TriggerWords);

    public IInferenceClientManager ClientManager { get; }

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
                ClipWeight = ClipWeight,
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

    private bool FilterCompatibleLoras(HybridModelFile? lora)
    {
        if (!settingsManager.Settings.FilterExtraNetworksByBaseModel)
            return true;

        return modelCompatChecker.IsLoraCompatibleWithBaseModel(lora, SelectedBaseModel) ?? true;
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
