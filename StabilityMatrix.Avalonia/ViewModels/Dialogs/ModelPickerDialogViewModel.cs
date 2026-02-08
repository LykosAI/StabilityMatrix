using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using AsyncAwaitBestPractices;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FuzzySharp;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.CheckpointManager;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

/// <summary>
/// Specifies which model source collection to use in the picker.
/// </summary>
public enum ModelPickerSource
{
    /// <summary>Checkpoints + UNet models (default for model selection)</summary>
    CheckpointAndUnet,

    /// <summary>LoRA models only</summary>
    Lora,

    /// <summary>VAE models only</summary>
    Vae,

    /// <summary>CLIP/Text encoder models only</summary>
    Clip,

    /// <summary>CLIP Vision models only</summary>
    ClipVision,
}

[View(typeof(ModelPickerDialog))]
[RegisterTransient<ModelPickerDialogViewModel>]
[ManagedService]
public partial class ModelPickerDialogViewModel : ContentDialogViewModelBase
{
    private readonly IInferenceClientManager clientManager;
    private readonly ISettingsManager settingsManager;
    private readonly CompositeDisposable propertySubscriptions = new();
    private LRUCache<string, ImmutableList<HybridModelFile>> searchCache = new(100);
    private IDisposable? modelSubscription;
    private ImmutableList<HybridModelFile> allModels = [];
    private int refreshRequestId;
    private HashSet<string> pendingSelectedBaseModels = [];
    private bool isApplyingSavedFilterState;
    private bool isDialogActive;

    /// <summary>
    /// Gets or sets the model source to use. Set before showing the dialog.
    /// </summary>
    public ModelPickerSource Source { get; set; } = ModelPickerSource.CheckpointAndUnet;

    [ObservableProperty]
    private string title = "Select Model";

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private HybridModelFile? selectedModel;

    [ObservableProperty]
    private IReadOnlyList<HybridModelFile> filteredModels = [];

    [ObservableProperty]
    private bool showCheckpointsOnly;

    [ObservableProperty]
    private bool showUnetsOnly;

    [ObservableProperty]
    private bool isGridView;

    [ObservableProperty]
    private bool showNsfwContent;

    public ObservableCollection<BaseModelOptionViewModel> BaseModelOptions { get; } = [];

    public IEnumerable<BaseModelOptionViewModel> SelectedBaseModelOptions =>
        BaseModelOptions.Where(x => x.IsSelected);

    public int ActiveFilterCount =>
        SelectedBaseModelOptions.Count() + (ShowCheckpointsOnly ? 1 : 0) + (ShowUnetsOnly ? 1 : 0);

    public bool HasActiveFilters => ActiveFilterCount > 0;
    public bool HasFilteredModels => FilteredModels.Count > 0;

    public string FilterButtonText => HasActiveFilters ? $"Filter ({ActiveFilterCount})" : "Filter";

    /// <summary>
    /// Whether to show the folder type filter buttons (Checkpoints/Diffusion Models).
    /// Only relevant for CheckpointAndUnet source.
    /// </summary>
    public bool ShowFolderTypeFilters => Source == ModelPickerSource.CheckpointAndUnet;

    public ModelPickerDialogViewModel(IInferenceClientManager clientManager, ISettingsManager settingsManager)
    {
        this.clientManager = clientManager;
        this.settingsManager = settingsManager;
        isGridView = settingsManager.Settings.ModelPickerIsGridView;
        showNsfwContent = settingsManager.Settings.ModelBrowserNsfwEnabled;

        // Subscribe to search text and filter changes
        propertySubscriptions.Add(
            Observable
                .FromEventPattern<PropertyChangedEventArgs>(this, nameof(PropertyChanged))
                .Where(x =>
                    x.EventArgs.PropertyName
                        is nameof(SearchText)
                            or nameof(ShowCheckpointsOnly)
                            or nameof(ShowUnetsOnly)
                )
                .Throttle(TimeSpan.FromMilliseconds(100))
                .ObserveOn(SynchronizationContext.Current!)
                .Subscribe(_ =>
                {
                    if (!isDialogActive)
                        return;

                    UpdateFilteredModels();
                    OnPropertyChanged(nameof(ActiveFilterCount));
                    OnPropertyChanged(nameof(HasActiveFilters));
                    OnPropertyChanged(nameof(FilterButtonText));
                    SaveFilterStateForCurrentSource();
                })
        );

        // Subscribe to base model option changes
        propertySubscriptions.Add(
            BaseModelOptions
                .ToObservableChangeSet()
                .AutoRefresh(x => x.IsSelected)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .ObserveOn(SynchronizationContext.Current!)
                .Subscribe(_ =>
                {
                    if (!isDialogActive)
                        return;

                    UpdateFilteredModels();
                    OnPropertyChanged(nameof(SelectedBaseModelOptions));
                    OnPropertyChanged(nameof(ActiveFilterCount));
                    OnPropertyChanged(nameof(HasActiveFilters));
                    OnPropertyChanged(nameof(FilterButtonText));
                    SaveFilterStateForCurrentSource();
                })
        );

        AddDisposable(propertySubscriptions);
    }

    partial void OnIsGridViewChanged(bool value)
    {
        settingsManager.Transaction(s => s.ModelPickerIsGridView = value);
    }

    partial void OnShowNsfwContentChanged(bool value)
    {
        settingsManager.Transaction(s => s.ModelBrowserNsfwEnabled = value);
    }

    partial void OnFilteredModelsChanged(IReadOnlyList<HybridModelFile> value)
    {
        OnPropertyChanged(nameof(HasFilteredModels));
    }

    partial void OnShowCheckpointsOnlyChanged(bool value)
    {
        if (value && ShowUnetsOnly)
        {
            ShowUnetsOnly = false;
        }
    }

    partial void OnShowUnetsOnlyChanged(bool value)
    {
        if (value && ShowCheckpointsOnly)
        {
            ShowCheckpointsOnly = false;
        }
    }

    public override void OnLoaded()
    {
        base.OnLoaded();
        isDialogActive = true;

        // Save caller-specified type filters before loading persisted state
        // (e.g., WanModelCardViewModel sets ShowUnetsOnly = true before opening)
        var preShowUnets = ShowUnetsOnly;
        var preShowCheckpoints = ShowCheckpointsOnly;

        LoadFilterStateForCurrentSource();

        // Re-apply caller-specified type filters (they take priority over saved state)
        if (preShowUnets)
            ShowUnetsOnly = true;
        if (preShowCheckpoints)
            ShowCheckpointsOnly = true;

        // Populate models in background after dialog appears to reduce opening hitch.
        Dispatcher.UIThread.Post(RefreshAllModels, DispatcherPriority.Background);

        // Subscribe to changes in the relevant collections
        var subscriptions = new List<IDisposable>();

        switch (Source)
        {
            case ModelPickerSource.CheckpointAndUnet:
                subscriptions.Add(
                    clientManager
                        .Models.ToObservableChangeSet<
                            IObservableCollection<HybridModelFile>,
                            HybridModelFile
                        >()
                        .Throttle(TimeSpan.FromMilliseconds(100))
                        .ObserveOn(SynchronizationContext.Current!)
                        .Subscribe(_ => RefreshAllModels())
                );
                subscriptions.Add(
                    clientManager
                        .UnetModels.ToObservableChangeSet<
                            IObservableCollection<HybridModelFile>,
                            HybridModelFile
                        >()
                        .Throttle(TimeSpan.FromMilliseconds(100))
                        .ObserveOn(SynchronizationContext.Current!)
                        .Subscribe(_ => RefreshAllModels())
                );
                break;

            case ModelPickerSource.Lora:
                subscriptions.Add(
                    clientManager
                        .LoraModels.ToObservableChangeSet<
                            IObservableCollection<HybridModelFile>,
                            HybridModelFile
                        >()
                        .Throttle(TimeSpan.FromMilliseconds(100))
                        .ObserveOn(SynchronizationContext.Current!)
                        .Subscribe(_ => RefreshAllModels())
                );
                break;

            case ModelPickerSource.Vae:
                subscriptions.Add(
                    clientManager
                        .VaeModels.ToObservableChangeSet<
                            IObservableCollection<HybridModelFile>,
                            HybridModelFile
                        >()
                        .Throttle(TimeSpan.FromMilliseconds(100))
                        .ObserveOn(SynchronizationContext.Current!)
                        .Subscribe(_ => RefreshAllModels())
                );
                break;

            case ModelPickerSource.Clip:
                subscriptions.Add(
                    clientManager
                        .ClipModels.ToObservableChangeSet<
                            IObservableCollection<HybridModelFile>,
                            HybridModelFile
                        >()
                        .Throttle(TimeSpan.FromMilliseconds(100))
                        .ObserveOn(SynchronizationContext.Current!)
                        .Subscribe(_ => RefreshAllModels())
                );
                break;

            case ModelPickerSource.ClipVision:
                subscriptions.Add(
                    clientManager
                        .ClipVisionModels.ToObservableChangeSet<
                            IObservableCollection<HybridModelFile>,
                            HybridModelFile
                        >()
                        .Throttle(TimeSpan.FromMilliseconds(100))
                        .ObserveOn(SynchronizationContext.Current!)
                        .Subscribe(_ => RefreshAllModels())
                );
                break;
        }

        modelSubscription?.Dispose();
        modelSubscription = new CompositeDisposable(subscriptions);
    }

    private void RefreshAllModels()
    {
        if (!isDialogActive)
            return;

        RefreshAllModelsAsync().SafeFireAndForget();
    }

    private async Task RefreshAllModelsAsync()
    {
        var requestId = Interlocked.Increment(ref refreshRequestId);

        var sortedModels = await Task.Run(() =>
        {
            IEnumerable<HybridModelFile> models = Source switch
            {
                ModelPickerSource.CheckpointAndUnet => clientManager.Models.Concat(clientManager.UnetModels),
                ModelPickerSource.Lora => clientManager.LoraModels,
                ModelPickerSource.Vae => clientManager.VaeModels,
                ModelPickerSource.Clip => clientManager.ClipModels,
                ModelPickerSource.ClipVision => clientManager.ClipVisionModels,
                _ => [],
            };

            return models
                .OrderBy(m => m.ShortDisplayName, StringComparer.OrdinalIgnoreCase)
                .ToImmutableList();
        });

        // Ignore stale refreshes if a newer one was queued while this was running.
        if (requestId != refreshRequestId)
        {
            return;
        }

        if (!isDialogActive)
        {
            return;
        }

        allModels = sortedModels;
        UpdateAvailableBaseModels();
        UpdateFilteredModels();
    }

    public override void OnUnloaded()
    {
        base.OnUnloaded();
        isDialogActive = false;
        Interlocked.Increment(ref refreshRequestId);
        SaveFilterStateForCurrentSource();
        modelSubscription?.Dispose();
        modelSubscription = null;

        // Release only internal heavy references. Avoid touching bound collections here,
        // because clearing ItemsSource during close can null out SelectedModel before caller reads it.
        allModels = [];
        searchCache = new LRUCache<string, ImmutableList<HybridModelFile>>(100);
    }

    private void UpdateAvailableBaseModels()
    {
        var baseModels = allModels
            .Where(m => m.Local?.ConnectedModelInfo?.BaseModel != null)
            .Select(m => m.Local!.ConnectedModelInfo!.BaseModel!)
            .Distinct()
            .OrderBy(b => b)
            .ToList();

        // Add "Unknown" for models without metadata
        if (allModels.Any(m => m.Local?.ConnectedModelInfo?.BaseModel == null))
        {
            baseModels.Add("Unknown");
        }

        // Update BaseModelOptions collection, preserving selection state
        var existingSelections = BaseModelOptions
            .Where(x => x.IsSelected)
            .Select(x => x.ModelType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (existingSelections.Count == 0 && pendingSelectedBaseModels.Count > 0)
        {
            existingSelections = new HashSet<string>(
                pendingSelectedBaseModels,
                StringComparer.OrdinalIgnoreCase
            );
        }

        BaseModelOptions.Clear();
        foreach (var baseModel in baseModels)
        {
            BaseModelOptions.Add(
                new BaseModelOptionViewModel
                {
                    ModelType = baseModel,
                    IsSelected = existingSelections.Contains(baseModel),
                }
            );
        }
    }

    private string GetSourceKey() => Source.ToString();

    private void LoadFilterStateForCurrentSource()
    {
        var states = settingsManager.Settings.ModelPickerFilterStates;
        if (states is null || !states.TryGetValue(GetSourceKey(), out var state) || state is null)
        {
            pendingSelectedBaseModels = [];
            return;
        }

        isApplyingSavedFilterState = true;
        try
        {
            SearchText = state.SearchText ?? string.Empty;
            ShowCheckpointsOnly = state.ShowCheckpointsOnly;
            ShowUnetsOnly = state.ShowUnetsOnly;
            pendingSelectedBaseModels = (state.SelectedBaseModels ?? []).ToHashSet(
                StringComparer.OrdinalIgnoreCase
            );
        }
        finally
        {
            isApplyingSavedFilterState = false;
        }
    }

    private void SaveFilterStateForCurrentSource()
    {
        if (isApplyingSavedFilterState)
            return;

        var selectedBaseModels =
            BaseModelOptions.Count > 0
                ? SelectedBaseModelOptions.Select(x => x.ModelType).ToList()
                : pendingSelectedBaseModels.ToList();

        var state = new ModelPickerFilterState
        {
            SearchText = SearchText.Trim(),
            ShowCheckpointsOnly = ShowCheckpointsOnly,
            ShowUnetsOnly = ShowUnetsOnly,
            SelectedBaseModels = selectedBaseModels,
        };

        settingsManager.Transaction(s =>
        {
            s.ModelPickerFilterStates ??= [];
            s.ModelPickerFilterStates[GetSourceKey()] = state;
        });
    }

    private void UpdateFilteredModels()
    {
        var models = allModels.AsEnumerable();

        // Apply base model filter
        var selectedBaseModels = SelectedBaseModelOptions.Select(x => x.ModelType).ToList();
        if (selectedBaseModels.Count > 0)
        {
            models = models.Where(m =>
            {
                var baseModel = m.Local?.ConnectedModelInfo?.BaseModel;
                if (baseModel == null)
                {
                    return selectedBaseModels.Contains("Unknown");
                }
                return selectedBaseModels.Contains(baseModel);
            });
        }

        // Apply folder type filter
        if (ShowCheckpointsOnly)
        {
            models = models.Where(m => m.Local?.SharedFolderType == SharedFolderType.StableDiffusion);
        }
        else if (ShowUnetsOnly)
        {
            models = models.Where(m => m.Local?.SharedFolderType == SharedFolderType.DiffusionModels);
        }

        var modelList = models.ToList();

        // Apply search filter
        var query = SearchText.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            // Check cache
            var cacheKey =
                $"{refreshRequestId}|{query}|{string.Join(",", selectedBaseModels)}|{ShowCheckpointsOnly}|{ShowUnetsOnly}";
            if (searchCache.Get(cacheKey, out var cachedResults))
            {
                FilteredModels = cachedResults!;
                return;
            }

            var results = modelList
                .Select(m =>
                {
                    var modelSearchText = m.DetailedSearchText;
                    var weightedScore = Fuzz.WeightedRatio(query, modelSearchText);
                    var partialScore = Fuzz.PartialRatio(query, modelSearchText);
                    var score = Math.Max(weightedScore, partialScore);
                    var contains = modelSearchText.Contains(query, StringComparison.OrdinalIgnoreCase);
                    return (Model: m, Score: score, Contains: contains);
                })
                .Where(x => x.Contains || x.Score >= 70)
                .OrderByDescending(x => x.Contains)
                .ThenByDescending(x => x.Score)
                .Select(x => x.Model)
                .ToImmutableList();

            searchCache.Add(cacheKey, results);
            FilteredModels = results;
        }
        else
        {
            FilteredModels = modelList.ToImmutableList();
        }
    }

    [RelayCommand]
    private void ClearOrSelectAllBaseModels()
    {
        var anySelected = BaseModelOptions.Any(x => x.IsSelected);
        foreach (var option in BaseModelOptions)
        {
            option.IsSelected = !anySelected;
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        foreach (var option in BaseModelOptions)
        {
            option.IsSelected = false;
        }
        ShowCheckpointsOnly = false;
        ShowUnetsOnly = false;
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void SelectModel(HybridModelFile? model)
    {
        if (model != null)
        {
            SelectedModel = model;
            OnPrimaryButtonClick();
        }
    }

    [RelayCommand]
    private void SetSelectedModel(HybridModelFile? model)
    {
        SelectedModel = model;
    }

    public override BetterContentDialog GetDialog()
    {
        var dialog = base.GetDialog();

        dialog.MinDialogWidth = 700;
        dialog.MaxDialogWidth = 900;
        dialog.MinDialogHeight = 500;
        dialog.MaxDialogHeight = 700;
        dialog.IsFooterVisible = false;
        dialog.CloseOnClickOutside = true;
        // Disable dialog's internal scrolling - let the ListBox handle it
        dialog.ContentVerticalScrollBarVisibility = global::Avalonia
            .Controls
            .Primitives
            .ScrollBarVisibility
            .Disabled;

        return dialog;
    }
}
