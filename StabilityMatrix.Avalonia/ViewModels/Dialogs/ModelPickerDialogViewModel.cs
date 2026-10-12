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
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models.Inference;
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
    private HashSet<string> pendingSelectedCategories = [];
    private bool isApplyingSavedFilterState;
    private bool isDialogActive;
    private IReadOnlyDictionary<string, LocalFileSortData> fileSortDataById = ImmutableDictionary<
        string,
        LocalFileSortData
    >.Empty;

    private readonly record struct LocalFileSortData(
        long FileSize,
        DateTimeOffset Created,
        DateTimeOffset LastModified
    );

    /// <summary>
    /// Gets or sets the model source to use. Set before showing the dialog.
    /// </summary>
    public ModelPickerSource Source { get; set; } = ModelPickerSource.CheckpointAndUnet;

    /// <summary>
    /// Optional workflow hint from Inference model cards. Used to open near compatible models.
    /// </summary>
    public InferenceWorkflowProfile PreferredWorkflowProfile { get; set; } = InferenceWorkflowProfile.Auto;

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

    [ObservableProperty]
    public partial CheckpointSortMode SelectedSortOption { get; set; }

    [ObservableProperty]
    public partial ListSortDirection SelectedSortDirection { get; set; }

    /// <summary>
    /// Sort modes applicable to picker entries. <see cref="CheckpointSortMode.UpdateAvailable"/>
    /// is excluded because update state is not tracked on <see cref="HybridModelFile"/>.
    /// </summary>
    public List<CheckpointSortMode> SortOptions { get; } =
        Enum.GetValues<CheckpointSortMode>().Where(x => x != CheckpointSortMode.UpdateAvailable).ToList();

    public List<ListSortDirection> SortDirections { get; } = Enum.GetValues<ListSortDirection>().ToList();

    public ObservableCollection<BaseModelOptionViewModel> BaseModelOptions { get; } = [];

    public IEnumerable<BaseModelOptionViewModel> SelectedBaseModelOptions =>
        BaseModelOptions.Where(x => x.IsSelected);

    /// <summary>
    /// User categories present on the models of the current source. Empty hides the section.
    /// </summary>
    public ObservableCollection<CategoryOptionViewModel> CategoryOptions { get; } = [];

    public IEnumerable<CategoryOptionViewModel> SelectedCategoryOptions =>
        CategoryOptions.Where(x => x.IsSelected);

    public bool HasCategoryOptions => CategoryOptions.Count > 0;

    /// <summary>
    /// Category registry (name + colour) for chip rendering in the list templates.
    /// </summary>
    public IReadOnlyList<ModelCategory> CategoryPalette => settingsManager.Settings.ModelCategories;

    public int SelectedCategoryCount => SelectedCategoryOptions.Count();

    public string CategoriesButtonText =>
        SelectedCategoryCount > 0
            ? $"{Resources.Label_Categories} ({SelectedCategoryCount})"
            : Resources.Label_Categories;

    /// <summary>
    /// Number of base models selected under the Base Model button.
    /// </summary>
    public int SelectedBaseModelCount => SelectedBaseModelOptions.Count();

    public bool HasActiveFilters =>
        SelectedBaseModelCount > 0 || ShowCheckpointsOnly || ShowUnetsOnly || SelectedCategoryCount > 0;
    public bool HasFilteredModels => FilteredModels.Count > 0;

    public string BaseModelButtonText =>
        SelectedBaseModelCount > 0
            ? $"{Resources.Label_BaseModel} ({SelectedBaseModelCount})"
            : Resources.Label_BaseModel;

    /// <summary>
    /// The "All" segment of the folder type selector: true when neither type filter is on.
    /// Setting it to true clears both; setting it to false does nothing, since another segment
    /// turning on is what clears it.
    /// </summary>
    public bool ShowAllFolderTypes
    {
        get => !ShowCheckpointsOnly && !ShowUnetsOnly;
        set
        {
            if (!value)
                return;

            ShowCheckpointsOnly = false;
            ShowUnetsOnly = false;
        }
    }

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

        isApplyingSavedFilterState = true;
        try
        {
            SelectedSortOption = settingsManager.Settings.ModelPickerSortMode;
            SelectedSortDirection = settingsManager.Settings.ModelPickerSortDirection;
        }
        finally
        {
            isApplyingSavedFilterState = false;
        }

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
                    OnPropertyChanged(nameof(SelectedBaseModelCount));
                    OnPropertyChanged(nameof(HasActiveFilters));
                    OnPropertyChanged(nameof(BaseModelButtonText));
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
                    OnPropertyChanged(nameof(SelectedBaseModelCount));
                    OnPropertyChanged(nameof(HasActiveFilters));
                    OnPropertyChanged(nameof(BaseModelButtonText));
                    SaveFilterStateForCurrentSource();
                })
        );

        // Subscribe to category option changes
        propertySubscriptions.Add(
            CategoryOptions
                .ToObservableChangeSet()
                .AutoRefresh(x => x.IsSelected)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .ObserveOn(SynchronizationContext.Current!)
                .Subscribe(_ =>
                {
                    if (!isDialogActive)
                        return;

                    UpdateFilteredModels();
                    OnPropertyChanged(nameof(SelectedCategoryOptions));
                    OnPropertyChanged(nameof(SelectedCategoryCount));
                    OnPropertyChanged(nameof(CategoriesButtonText));
                    OnPropertyChanged(nameof(HasActiveFilters));
                    SaveFilterStateForCurrentSource();
                })
        );

        AddDisposable(propertySubscriptions);
    }

    partial void OnIsGridViewChanged(bool value)
    {
        settingsManager.Transaction(s => s.ModelPickerIsGridView = value);
    }

    partial void OnSelectedSortOptionChanged(CheckpointSortMode value)
    {
        if (isApplyingSavedFilterState)
            return;

        settingsManager.Transaction(s => s.ModelPickerSortMode = value);

        if (isDialogActive)
        {
            UpdateFilteredModels();
        }
    }

    partial void OnSelectedSortDirectionChanged(ListSortDirection value)
    {
        if (isApplyingSavedFilterState)
            return;

        settingsManager.Transaction(s => s.ModelPickerSortDirection = value);

        if (isDialogActive)
        {
            UpdateFilteredModels();
        }
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

        OnPropertyChanged(nameof(ShowAllFolderTypes));
    }

    partial void OnShowUnetsOnlyChanged(bool value)
    {
        if (value && ShowCheckpointsOnly)
        {
            ShowCheckpointsOnly = false;
        }

        OnPropertyChanged(nameof(ShowAllFolderTypes));
    }

    public override void OnLoaded()
    {
        base.OnLoaded();
        isDialogActive = true;

        // Save caller-specified type filters before loading persisted state
        // (e.g., VideoModelCardViewModel sets ShowUnetsOnly = true before opening)
        var preShowUnets = ShowUnetsOnly;
        var preShowCheckpoints = ShowCheckpointsOnly;

        LoadFilterStateForCurrentSource();

        // Re-apply caller-specified type filters (they take priority over saved state)
        if (preShowUnets)
            ShowUnetsOnly = true;
        if (preShowCheckpoints)
            ShowCheckpointsOnly = true;

        ApplyPreferredWorkflowProfileFilters();

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

        var modelsDirectory = settingsManager.IsLibraryDirSet ? settingsManager.ModelsDirectory : null;

        var (sortedModels, sortData) = await Task.Run(() =>
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

            var sorted = models
                .OrderBy(m => m.ShortDisplayName, StringComparer.OrdinalIgnoreCase)
                .ToImmutableList();

            return (sorted, BuildFileSortData(sorted, modelsDirectory));
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
        fileSortDataById = sortData;
        UpdateAvailableBaseModels();
        UpdateAvailableCategories();
        UpdateFilteredModels();
    }

    /// <summary>
    /// Stats local model files for size/date sort keys. Remote and placeholder entries
    /// have no backing file and fall back to <see langword="default"/> sort data.
    /// </summary>
    private static Dictionary<string, LocalFileSortData> BuildFileSortData(
        IEnumerable<HybridModelFile> models,
        string? modelsDirectory
    )
    {
        var sortData = new Dictionary<string, LocalFileSortData>();

        if (modelsDirectory is null)
            return sortData;

        foreach (var model in models)
        {
            if (model.Local is not { } local)
                continue;

            var fileInfo = new FileInfo(local.GetFullPath(modelsDirectory));
            if (!fileInfo.Exists)
                continue;

            sortData[model.GetId()] = new LocalFileSortData(
                fileInfo.Length,
                fileInfo.CreationTime,
                fileInfo.LastWriteTime
            );
        }

        return sortData;
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
        fileSortDataById = ImmutableDictionary<string, LocalFileSortData>.Empty;
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

    private void UpdateAvailableCategories()
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var counts = new Dictionary<string, int>(comparer);
        foreach (var category in allModels.SelectMany(m => m.Local?.Categories ?? []))
        {
            counts[category] = counts.GetValueOrDefault(category) + 1;
        }

        var existingSelections = SelectedCategoryOptions.Select(x => x.Name).ToHashSet(comparer);
        if (existingSelections.Count == 0 && pendingSelectedCategories.Count > 0)
        {
            existingSelections = new HashSet<string>(pendingSelectedCategories, comparer);
        }

        // Registry order first (that's the user's order), then unregistered names alphabetically
        var registry = CategoryPalette;
        var ordered = registry
            .Where(c => counts.ContainsKey(c.Name))
            .Select(c => (c.Name, c.Color))
            .Concat(
                counts
                    .Keys.Where(name => !registry.Any(c => comparer.Equals(c.Name, name)))
                    .OrderBy(name => name, comparer)
                    .Select(name => (Name: name, Color: (string?)null))
            );

        CategoryOptions.Clear();
        foreach (var (name, color) in ordered)
        {
            CategoryOptions.Add(
                new CategoryOptionViewModel
                {
                    Name = name,
                    ColorHex = color,
                    ColorBrush = ModelCategoryStyle.SwatchBrush(color),
                    Count = counts[name],
                    IsSelected = existingSelections.Contains(name),
                }
            );
        }

        OnPropertyChanged(nameof(HasCategoryOptions));
        OnPropertyChanged(nameof(CategoryPalette));
        OnPropertyChanged(nameof(SelectedCategoryCount));
        OnPropertyChanged(nameof(CategoriesButtonText));
        OnPropertyChanged(nameof(HasActiveFilters));
    }

    private string GetSourceKey() => Source.ToString();

    private void LoadFilterStateForCurrentSource()
    {
        var states = settingsManager.Settings.ModelPickerFilterStates;
        if (states is null || !states.TryGetValue(GetSourceKey(), out var state) || state is null)
        {
            pendingSelectedBaseModels = [];
            pendingSelectedCategories = [];
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
            pendingSelectedCategories = (state.SelectedCategories ?? []).ToHashSet(
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
        if (isApplyingSavedFilterState || PreferredWorkflowProfile != InferenceWorkflowProfile.Auto)
            return;

        var selectedBaseModels =
            BaseModelOptions.Count > 0
                ? SelectedBaseModelOptions.Select(x => x.ModelType).ToList()
                : pendingSelectedBaseModels.ToList();

        var selectedCategories =
            CategoryOptions.Count > 0
                ? SelectedCategoryOptions.Select(x => x.Name).ToList()
                : pendingSelectedCategories.ToList();

        var state = new ModelPickerFilterState
        {
            SearchText = SearchText.Trim(),
            ShowCheckpointsOnly = ShowCheckpointsOnly,
            ShowUnetsOnly = ShowUnetsOnly,
            SelectedBaseModels = selectedBaseModels,
            SelectedCategories = selectedCategories,
        };

        settingsManager.Transaction(s =>
        {
            s.ModelPickerFilterStates ??= [];
            s.ModelPickerFilterStates[GetSourceKey()] = state;
        });
    }

    private void ApplyPreferredWorkflowProfileFilters()
    {
        if (
            Source is not ModelPickerSource.CheckpointAndUnet
            || PreferredWorkflowProfile == InferenceWorkflowProfile.Auto
            || PreferredWorkflowProfile == InferenceWorkflowProfile.Custom
        )
        {
            return;
        }

        if (PreferredWorkflowProfile == InferenceWorkflowProfile.DefaultCheckpoint)
        {
            ShowCheckpointsOnly = true;
            return;
        }

        ShowUnetsOnly = true;
        pendingSelectedBaseModels = PreferredWorkflowProfile.PreferredBaseModels.ToHashSet(
            StringComparer.OrdinalIgnoreCase
        );
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

        // Apply category filter (any-of)
        var selectedCategories = SelectedCategoryOptions.Select(x => x.Name).ToList();
        if (selectedCategories.Count > 0)
        {
            models = models.Where(m =>
                m.Local?.Categories.Any(c => selectedCategories.Contains(c, StringComparer.OrdinalIgnoreCase))
                ?? false
            );
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
            var selectedBaseModelsKey = string.Join(
                ",",
                selectedBaseModels.OrderBy(x => x, StringComparer.Ordinal)
            );
            var selectedCategoriesKey = string.Join(
                ",",
                selectedCategories.OrderBy(x => x, StringComparer.Ordinal)
            );
            var cacheKey =
                $"{refreshRequestId}|{query}|{selectedBaseModelsKey}|{selectedCategoriesKey}|{ShowCheckpointsOnly}|{ShowUnetsOnly}";
            if (searchCache.Get(cacheKey, out var cachedResults))
            {
                FilteredModels = cachedResults!;
                return;
            }

            // Relevance ordering wins while a query is active; the chosen sort applies otherwise.
            var results = modelList
                .Select(m => (Model: m, Result: FuzzySearchHelper.Match(query, m.DetailedSearchText)))
                .Where(x => x.Result.IsMatch)
                .OrderByDescending(x => x.Result.ContainsQuery)
                .ThenByDescending(x => x.Result.Score)
                .Select(x => x.Model)
                .ToImmutableList();

            searchCache.Add(cacheKey, results);
            FilteredModels = results;
        }
        else
        {
            FilteredModels = SortModels(modelList);
        }
    }

    private ImmutableList<HybridModelFile> SortModels(IEnumerable<HybridModelFile> models)
    {
        var ascending = SelectedSortDirection == ListSortDirection.Ascending;

        // Placeholder entries (None / Default) always sort first regardless of mode.
        var placeholdersFirst = models.OrderByDescending(m => m.IsNone || m.IsDefault);

        var sorted = SelectedSortOption switch
        {
            CheckpointSortMode.FileName => ThenSortBy(m => m.FileName, StringComparer.OrdinalIgnoreCase),
            CheckpointSortMode.Title => ThenSortBy(m => m.SortKey, StringComparer.OrdinalIgnoreCase),
            CheckpointSortMode.BaseModel => ThenSortBy(
                m => m.Local?.ConnectedModelInfo?.BaseModel ?? string.Empty,
                StringComparer.OrdinalIgnoreCase
            ),
            CheckpointSortMode.SharedFolderType => ThenSortBy(m => m.Local?.SharedFolderType),
            CheckpointSortMode.FileSize => ThenSortBy(m => GetFileSortData(m).FileSize),
            CheckpointSortMode.Created => ThenSortBy(m => GetFileSortData(m).Created),
            CheckpointSortMode.LastModified => ThenSortBy(m => GetFileSortData(m).LastModified),
            _ => ThenSortBy(m => m.SortKey, StringComparer.OrdinalIgnoreCase),
        };

        return sorted.ThenBy(m => m.SortKey, StringComparer.OrdinalIgnoreCase).ToImmutableList();

        IOrderedEnumerable<HybridModelFile> ThenSortBy<TKey>(
            Func<HybridModelFile, TKey> keySelector,
            IComparer<TKey>? comparer = null
        ) =>
            ascending
                ? placeholdersFirst.ThenBy(keySelector, comparer)
                : placeholdersFirst.ThenByDescending(keySelector, comparer);
    }

    private LocalFileSortData GetFileSortData(HybridModelFile model) =>
        fileSortDataById.GetValueOrDefault(model.GetId());

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
    private void ClearCategories()
    {
        foreach (var option in CategoryOptions)
        {
            option.IsSelected = false;
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        foreach (var option in BaseModelOptions)
        {
            option.IsSelected = false;
        }
        foreach (var option in CategoryOptions)
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
