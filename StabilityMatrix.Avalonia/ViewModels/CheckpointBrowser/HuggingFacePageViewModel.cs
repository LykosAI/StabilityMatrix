using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models.HuggingFace;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.HuggingFacePage;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.HuggingFace;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

/// <summary>
/// What the page is currently showing.
/// </summary>
public enum HuggingFaceBrowserMode
{
    /// <summary>The curated, bundled list of recommended models (empty search box).</summary>
    Curated,

    /// <summary>Live model search results for a free-text query.</summary>
    SearchResults,

    /// <summary>The file tree of a single repository.</summary>
    RepoFiles,
}

[View(typeof(Views.HuggingFacePage))]
[RegisterSingleton<HuggingFacePageViewModel>]
public partial class HuggingFacePageViewModel : TabViewModelBase
{
    private readonly ITrackedDownloadService trackedDownloadService;
    private readonly ISettingsManager settingsManager;
    private readonly INotificationService notificationService;
    private readonly IHuggingFaceApi huggingFaceApi;
    private readonly ISecretsManager secretsManager;

    public SourceCache<HuggingfaceItem, string> ItemsCache { get; } =
        new(i => i.RepositoryPath + i.ModelName);

    public IObservableCollection<CategoryViewModel> Categories { get; set; } =
        new ObservableCollectionExtended<CategoryViewModel>();

    /// <summary>Live model search results (<see cref="HuggingFaceBrowserMode.SearchResults"/>).</summary>
    public ObservableCollection<HuggingFaceSearchResultViewModel> SearchResults { get; } = new();

    /// <summary>All files of the currently browsed repo (unfiltered). Source of truth for selection/import.</summary>
    private readonly List<HuggingFaceFileViewModel> allRepoFiles = new();

    /// <summary>Filtered flat list of repo files shown in the flat view.</summary>
    public AvaloniaList<HuggingFaceFileViewModel> RepoFiles { get; } = new();

    /// <summary>Top-level nodes of the structured folder-tree view of the current repo.</summary>
    public AvaloniaList<HuggingFaceTreeNodeViewModel> RepoTreeRoot { get; } = new();

    /// <summary>Sort options offered for live search.</summary>
    public IReadOnlyList<HuggingFaceSearchSort> SearchSortOptions { get; } =
        Enum.GetValues<HuggingFaceSearchSort>();

    /// <summary>When true, the repo is shown as an expandable folder tree; otherwise a flat list.</summary>
    [ObservableProperty]
    private bool isStructuredView = true;

    /// <summary>Text filter applied to the current repo's files (by name/path).</summary>
    [ObservableProperty]
    private string? repoFilter;

    /// <summary>When true, already-installed files are hidden from the repo views.</summary>
    [ObservableProperty]
    private bool hideInstalled;

    /// <summary>Sort order for live search results.</summary>
    [ObservableProperty]
    private HuggingFaceSearchSort searchSort = HuggingFaceSearchSort.Downloads;

    /// <summary>Summary of the current selection, e.g. "4 files · 45.2 GB".</summary>
    [ObservableProperty]
    private string? selectionSummary;

    /// <summary>Set when the selected repo files won't fit in the models directory's free space.</summary>
    [ObservableProperty]
    private string? diskSpaceWarning;

    [ObservableProperty]
    private int numSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCuratedVisible))]
    [NotifyPropertyChangedFor(nameof(IsSearchResultsVisible))]
    [NotifyPropertyChangedFor(nameof(IsRepoFilesVisible))]
    private HuggingFaceBrowserMode mode = HuggingFaceBrowserMode.Curated;

    [ObservableProperty]
    private string? searchQuery;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? currentRepoId;

    public bool IsCuratedVisible => Mode == HuggingFaceBrowserMode.Curated;
    public bool IsSearchResultsVisible => Mode == HuggingFaceBrowserMode.SearchResults;
    public bool IsRepoFilesVisible => Mode == HuggingFaceBrowserMode.RepoFiles;

    private CancellationTokenSource? queryCts;

    public HuggingFacePageViewModel(
        ITrackedDownloadService trackedDownloadService,
        ISettingsManager settingsManager,
        INotificationService notificationService,
        IHuggingFaceApi huggingFaceApi,
        ISecretsManager secretsManager
    )
    {
        this.trackedDownloadService = trackedDownloadService;
        this.settingsManager = settingsManager;
        this.notificationService = notificationService;
        this.huggingFaceApi = huggingFaceApi;
        this.secretsManager = secretsManager;

        // Restore persisted UI prefs (set backing fields directly to avoid triggering persistence/rebuilds).
        isStructuredView = settingsManager.Settings.HuggingFaceUseStructuredView;
        hideInstalled = settingsManager.Settings.HuggingFaceHideInstalled;
        searchSort = settingsManager.Settings.HuggingFaceSearchSort;

        ItemsCache
            .Connect()
            .DeferUntilLoaded()
            .Group(i => i.ModelCategory)
            .Transform(g => new CategoryViewModel(
                g.Cache.Items,
                Design.IsDesignMode ? string.Empty : settingsManager.ModelsDirectory
            )
            {
                Title = g.Key.GetDescription() ?? g.Key.ToString(),
            })
            .SortBy(vm => vm.Title ?? "")
            .Bind(Categories)
            .WhenAnyPropertyChanged()
            .Throttle(TimeSpan.FromMilliseconds(50))
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(_ => UpdateNumSelected());
    }

    public override void OnLoaded()
    {
        if (ItemsCache.Count > 0)
            return;

        using var reader = new StreamReader(Assets.HfPackagesJson.Open());
        var packages =
            JsonSerializer.Deserialize<IReadOnlyList<HuggingfaceItem>>(
                reader.ReadToEnd(),
                new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }
            ) ?? throw new InvalidOperationException("Failed to read hf-packages.json");

        ItemsCache.EditDiff(packages, (a, b) => a.RepositoryPath == b.RepositoryPath);
    }

    /// <summary>
    /// Debounce search-box changes, then either show curated content, search, or load a repo tree.
    /// </summary>
    partial void OnSearchQueryChanged(string? value)
    {
        queryCts?.Cancel();
        queryCts = new CancellationTokenSource();
        _ = DispatchQueryAsync(value, queryCts.Token);
    }

    partial void OnModeChanged(HuggingFaceBrowserMode value) => UpdateNumSelected();

    partial void OnIsStructuredViewChanged(bool value) =>
        settingsManager.Transaction(s => s.HuggingFaceUseStructuredView = value);

    partial void OnHideInstalledChanged(bool value)
    {
        settingsManager.Transaction(s => s.HuggingFaceHideInstalled = value);
        RebuildRepoViews();
    }

    partial void OnRepoFilterChanged(string? value) => RebuildRepoViews();

    partial void OnSearchSortChanged(HuggingFaceSearchSort value)
    {
        settingsManager.Transaction(s => s.HuggingFaceSearchSort = value);

        if (Mode == HuggingFaceBrowserMode.SearchResults && !string.IsNullOrWhiteSpace(SearchQuery))
        {
            queryCts?.Cancel();
            queryCts = new CancellationTokenSource();
            _ = PerformSearchAsync(SearchQuery.Trim(), queryCts.Token);
        }
    }

    private async Task DispatchQueryAsync(string? value, CancellationToken ct)
    {
        try
        {
            await Task.Delay(300, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ct.IsCancellationRequested)
            return;

        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            Mode = HuggingFaceBrowserMode.Curated;
            // No request runs here, and a superseded request won't clear this (see finally guards),
            // so clear it explicitly.
            IsLoading = false;
            return;
        }

        if (HuggingFaceFolderInference.TryParseRepoId(value, out var repoId))
        {
            await LoadRepoAsync(repoId, ct);
        }
        else
        {
            await PerformSearchAsync(value.Trim(), ct);
        }
    }

    private async Task<string?> GetAuthHeaderAsync()
    {
        var secrets = await secretsManager.SafeLoadAsync();
        return string.IsNullOrWhiteSpace(secrets.HuggingFaceToken)
            ? null
            : $"Bearer {secrets.HuggingFaceToken}";
    }

    private async Task PerformSearchAsync(string query, CancellationToken ct)
    {
        IsLoading = true;
        try
        {
            var auth = await GetAuthHeaderAsync();
            var response = await huggingFaceApi.SearchModelsAsync(
                query,
                MapSort(SearchSort),
                -1,
                30,
                null,
                auth,
                ct
            );

            if (ct.IsCancellationRequested)
                return;

            SearchResults.Clear();

            if (!response.IsSuccessStatusCode || response.Content is null)
            {
                ErrorMessage = $"Search failed ({(int)response.StatusCode})";
            }
            else
            {
                foreach (var entry in response.Content)
                {
                    SearchResults.Add(
                        new HuggingFaceSearchResultViewModel
                        {
                            Entry = entry,
                            OnSelected = SelectSearchResult,
                        }
                    );
                }
            }

            Mode = HuggingFaceBrowserMode.SearchResults;
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            ErrorMessage = e.Message;
            Mode = HuggingFaceBrowserMode.SearchResults;
        }
        finally
        {
            // Only clear the spinner if we're still the active request — a superseded
            // (cancelled) request must not turn off loading while a newer one runs.
            if (ct == queryCts?.Token)
                IsLoading = false;
        }
    }

    private static string MapSort(HuggingFaceSearchSort sort) =>
        sort switch
        {
            HuggingFaceSearchSort.Likes => "likes",
            HuggingFaceSearchSort.LastModified => "lastModified",
            _ => "downloads",
        };

    private void SelectSearchResult(string repoId)
    {
        queryCts?.Cancel();
        queryCts = new CancellationTokenSource();
        _ = LoadRepoAsync(repoId, queryCts.Token);
    }

    private async Task LoadRepoAsync(string repoId, CancellationToken ct)
    {
        var parts = repoId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return;

        IsLoading = true;
        ErrorMessage = null;
        CurrentRepoId = repoId;
        try
        {
            var auth = await GetAuthHeaderAsync();
            var response = await huggingFaceApi.GetRepoTreeAsync(parts[0], parts[1], "main", true, auth, ct);

            if (ct.IsCancellationRequested)
                return;

            ClearRepoFiles();

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                ErrorMessage =
                    "This repository is gated or private. Add your HuggingFace token in Settings → Accounts to access it.";
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                ErrorMessage = $"Repository '{repoId}' was not found.";
            }
            else if (!response.IsSuccessStatusCode || response.Content is null)
            {
                ErrorMessage = $"Couldn't load '{repoId}' ({(int)response.StatusCode})";
            }
            else
            {
                var modelsDir = Design.IsDesignMode ? string.Empty : settingsManager.ModelsDirectory;
                foreach (var entry in response.Content.Where(e => e.IsFile).OrderBy(e => e.Path))
                {
                    var vm = new HuggingFaceFileViewModel
                    {
                        Path = entry.Path,
                        RepositoryPath = repoId,
                        Size = entry.Size,
                        ModelsDir = modelsDir,
                        SelectedFolder = HuggingFaceFolderInference.Infer(entry.Path),
                    };
                    vm.PropertyChanged += RepoFileOnPropertyChanged;
                    allRepoFiles.Add(vm);
                }

                RebuildRepoViews();
            }

            Mode = HuggingFaceBrowserMode.RepoFiles;
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            ErrorMessage = e.Message;
            Mode = HuggingFaceBrowserMode.RepoFiles;
        }
        finally
        {
            // Only clear the spinner if we're still the active request — a superseded
            // (cancelled) request must not turn off loading while a newer one runs.
            if (ct == queryCts?.Token)
                IsLoading = false;
        }
    }

    private void RepoFileOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HuggingFaceFileViewModel.IsSelected))
            ScheduleSelectionUpdate();
    }

    private bool selectionUpdatePending;

    /// <summary>
    /// Coalesce many rapid selection changes (folder cascade, select-all, range select) into a
    /// single <see cref="UpdateNumSelected"/> pass so we don't recount + disk-check per file.
    /// </summary>
    private void ScheduleSelectionUpdate()
    {
        if (selectionUpdatePending)
            return;

        selectionUpdatePending = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                selectionUpdatePending = false;
                UpdateNumSelected();
            },
            DispatcherPriority.Background
        );
    }

    /// <summary>Anchor for shift-click range selection in the repo file views.</summary>
    private HuggingFaceFileViewModel? selectionAnchor;

    public void SetSelectionAnchor(HuggingFaceFileViewModel file) => selectionAnchor = file;

    /// <summary>
    /// Select every (non-installed) file between the anchor and <paramref name="file"/> in the
    /// currently displayed order. Used for shift-click range selection.
    /// </summary>
    public void SelectRangeTo(HuggingFaceFileViewModel file)
    {
        var targetIdx = RepoFiles.IndexOf(file);
        if (targetIdx < 0)
            return;

        var anchorIdx = selectionAnchor is null ? -1 : RepoFiles.IndexOf(selectionAnchor);
        if (anchorIdx < 0)
        {
            selectionAnchor = file;
            if (!file.Exists)
                file.IsSelected = true;
            return;
        }

        var lo = Math.Min(anchorIdx, targetIdx);
        var hi = Math.Max(anchorIdx, targetIdx);
        for (var i = lo; i <= hi; i++)
        {
            if (!RepoFiles[i].Exists)
                RepoFiles[i].IsSelected = true;
        }
    }

    private void ClearRepoFiles()
    {
        foreach (var file in allRepoFiles)
            file.PropertyChanged -= RepoFileOnPropertyChanged;
        allRepoFiles.Clear();
        RepoFiles.Clear();
        RepoTreeRoot.Clear();
    }

    /// <summary>
    /// Apply the current text filter + "hide installed" toggle to <see cref="allRepoFiles"/>,
    /// repopulating the flat list and the folder tree. Both views share the same file VMs so
    /// selection state stays in sync.
    /// </summary>
    private void RebuildRepoViews()
    {
        var filterText = RepoFilter?.Trim();
        var hasFilter = !string.IsNullOrEmpty(filterText);

        var filtered = allRepoFiles
            .Where(f =>
                (!HideInstalled || !f.Exists)
                && (!hasFilter || f.Path.Contains(filterText!, StringComparison.OrdinalIgnoreCase))
            )
            .ToList();

        // Replace contents in one batched notification each (cheap even for hundreds of rows).
        RepoFiles.Clear();
        RepoFiles.AddRange(filtered);

        BuildRepoTree(filtered, expandAll: hasFilter);
        UpdateNumSelected();
    }

    /// <summary>
    /// Build the folder tree from the given files, reusing the same file VMs as leaves so
    /// selection state is shared between the flat and structured views. The tree is assembled
    /// fully detached (plain lists), sorted, then committed to the bound collection in a single
    /// <c>AddRange</c> so the TreeView isn't churned per-item. Folders sort first.
    /// When <paramref name="expandAll"/> is true (e.g. an active filter) every folder is expanded
    /// so matches are visible; otherwise only top-level folders are expanded.
    /// </summary>
    private void BuildRepoTree(IReadOnlyList<HuggingFaceFileViewModel> files, bool expandAll)
    {
        // Snapshot which folders are currently expanded (by full path) so a rebuild
        // (filter / hide-installed toggle) keeps the user's expansion state.
        var previouslyExpanded = CollectExpandedFolderPaths();

        var rootNodes = new List<HuggingFaceTreeNodeViewModel>();
        var folderLookup = new Dictionary<string, HuggingFaceTreeNodeViewModel>();
        var childrenOf = new Dictionary<HuggingFaceTreeNodeViewModel, List<HuggingFaceTreeNodeViewModel>>();

        foreach (var file in files)
        {
            var segments = file.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentChildren = rootNodes;
            var pathAccum = string.Empty;

            for (var i = 0; i < segments.Length - 1; i++)
            {
                pathAccum = pathAccum.Length == 0 ? segments[i] : $"{pathAccum}/{segments[i]}";
                if (!folderLookup.TryGetValue(pathAccum, out var folder))
                {
                    folder = new HuggingFaceTreeNodeViewModel { Name = segments[i] };
                    folderLookup[pathAccum] = folder;
                    childrenOf[folder] = new List<HuggingFaceTreeNodeViewModel>();
                    currentChildren.Add(folder);
                }
                currentChildren = childrenOf[folder];
            }

            currentChildren.Add(new HuggingFaceTreeNodeViewModel { Name = segments[^1], File = file });
        }

        // Sort + commit each folder's children in one AddRange (still detached, so cheap).
        foreach (var (folder, children) in childrenOf)
        {
            children.Sort(CompareNodes);
            folder.Children.AddRange(children);
        }
        rootNodes.Sort(CompareNodes);

        if (expandAll)
        {
            foreach (var folder in folderLookup.Values)
                folder.IsExpanded = true;
        }
        else if (previouslyExpanded.Count > 0)
        {
            // Rebuild: restore the user's previous expansion state.
            foreach (var (path, folder) in folderLookup)
                folder.IsExpanded = previouslyExpanded.Contains(path);
        }
        else
        {
            // Initial load: expand top-level folders so the structure is visible; deeper stay collapsed.
            foreach (var node in rootNodes.Where(n => n.IsFolder))
                node.IsExpanded = true;
        }

        RepoTreeRoot.Clear();
        RepoTreeRoot.AddRange(rootNodes);
    }

    /// <summary>Collect the full paths of all currently-expanded folders in the live tree.</summary>
    private HashSet<string> CollectExpandedFolderPaths()
    {
        var result = new HashSet<string>();

        void Walk(IEnumerable<HuggingFaceTreeNodeViewModel> nodes, string prefix)
        {
            foreach (var node in nodes)
            {
                if (!node.IsFolder)
                    continue;

                var path = prefix.Length == 0 ? node.Name : $"{prefix}/{node.Name}";
                if (node.IsExpanded)
                    result.Add(path);
                Walk(node.Children, path);
            }
        }

        Walk(RepoTreeRoot, string.Empty);
        return result;
    }

    private static int CompareNodes(HuggingFaceTreeNodeViewModel a, HuggingFaceTreeNodeViewModel b)
    {
        // Folders before files, then by name (case-insensitive).
        if (a.IsFolder != b.IsFolder)
            return a.IsFolder ? -1 : 1;
        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateNumSelected()
    {
        NumSelected = Mode switch
        {
            HuggingFaceBrowserMode.RepoFiles => allRepoFiles.Count(f => f.IsSelected),
            HuggingFaceBrowserMode.SearchResults => 0,
            _ => Categories.Sum(c => c.NumSelected),
        };

        UpdateSelectionSummary();
    }

    /// <summary>
    /// Update the selection summary text and disk-space warning shown in the import bar.
    /// Sizes are only known for browsed repo files; curated items just show a count.
    /// </summary>
    private void UpdateSelectionSummary()
    {
        if (NumSelected == 0)
        {
            SelectionSummary = null;
            DiskSpaceWarning = null;
            return;
        }

        if (Mode != HuggingFaceBrowserMode.RepoFiles)
        {
            SelectionSummary = $"{NumSelected} selected";
            DiskSpaceWarning = null;
            return;
        }

        var fileWord = NumSelected == 1 ? "file" : "files";
        var totalBytes = allRepoFiles.Where(f => f.IsSelected).Sum(f => f.Size);
        SelectionSummary = $"{NumSelected} {fileWord} · {Size.FormatBase10Bytes(totalBytes)}";

        if (
            !Design.IsDesignMode
            && SystemInfo.GetDiskFreeSpaceBytes(settingsManager.ModelsDirectory) is { } freeBytes
            && totalBytes > freeBytes
        )
        {
            DiskSpaceWarning =
                $"Not enough free space — need {Size.FormatBase10Bytes(totalBytes)}, "
                + $"only {Size.FormatBase10Bytes(freeBytes)} available.";
        }
        else
        {
            DiskSpaceWarning = null;
        }
    }

    public void ClearSelection()
    {
        if (Mode == HuggingFaceBrowserMode.RepoFiles)
        {
            foreach (var file in allRepoFiles)
                file.IsSelected = false;
            UpdateNumSelected();
            return;
        }

        foreach (var category in Categories)
        {
            category.IsChecked = true;
            category.IsChecked = false;
        }
    }

    public void SelectAll()
    {
        if (Mode == HuggingFaceBrowserMode.RepoFiles)
        {
            foreach (var file in RepoFiles.Where(f => !f.Exists))
                file.IsSelected = true;
            UpdateNumSelected();
            return;
        }

        foreach (var category in Categories)
        {
            category.IsChecked = true;
        }
    }

    public void Refresh()
    {
        if (Mode == HuggingFaceBrowserMode.RepoFiles && CurrentRepoId is { } repoId)
        {
            queryCts?.Cancel();
            queryCts = new CancellationTokenSource();
            _ = LoadRepoAsync(repoId, queryCts.Token);
            return;
        }

        ItemsCache.Clear();
        OnLoaded();
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = null;
    }

    [RelayCommand]
    private async Task ImportSelected()
    {
        var summary = SelectionSummary;

        if (Mode == HuggingFaceBrowserMode.RepoFiles)
        {
            await ImportSelectedRepoFiles();
        }
        else
        {
            await ImportSelectedCurated();
        }

        notificationService.Show(
            Resources.Label_DownloadStarted,
            summary is null ? "Added to the download queue." : $"{summary} added to the download queue.",
            NotificationType.Information
        );
    }

    private async Task ImportSelectedCurated()
    {
        var selected = Categories.SelectMany(c => c.Items).Where(i => i.IsSelected).ToArray();

        foreach (var viewModel in selected)
        {
            foreach (var file in viewModel.Item.Files)
            {
                var url =
                    $"https://huggingface.co/{viewModel.Item.RepositoryPath}/resolve/main/{file}?download=true";
                var sharedFolderType = viewModel.Item.ModelCategory.ConvertTo<SharedFolderType>();
                var fileName = Path.GetFileName(file);

                if (
                    fileName.Equals("ae.safetensors", StringComparison.OrdinalIgnoreCase)
                    && viewModel.Item.ModelName == "HiDream I1 VAE"
                )
                {
                    fileName = "hidream_vae.safetensors";
                }

                var downloadPath = new FilePath(
                    Path.Combine(
                        Design.IsDesignMode ? string.Empty : settingsManager.ModelsDirectory,
                        sharedFolderType.ToString(),
                        viewModel.Item.Subfolder ?? string.Empty,
                        fileName
                    )
                );

                downloadPath.Directory?.Create();
                var download = trackedDownloadService.NewDownload(url, downloadPath);
                download.ProgressStateChanged += (_, e) =>
                {
                    if (e == ProgressState.Success)
                    {
                        viewModel.NotifyExistsChanged();
                    }
                };
                await trackedDownloadService.TryStartDownload(download);

                await Task.Delay(Random.Shared.Next(50, 100));
            }

            viewModel.IsSelected = false;
        }
    }

    private async Task ImportSelectedRepoFiles()
    {
        var selected = allRepoFiles.Where(f => f.IsSelected).ToArray();

        foreach (var file in selected)
        {
            var downloadPath = new FilePath(
                Path.Combine(
                    Design.IsDesignMode ? string.Empty : settingsManager.ModelsDirectory,
                    file.SelectedFolder.ToString(),
                    file.FileName
                )
            );

            downloadPath.Directory?.Create();
            var download = trackedDownloadService.NewDownload(file.DownloadUrl, downloadPath);
            download.ProgressStateChanged += (_, e) =>
            {
                if (e == ProgressState.Success)
                {
                    file.NotifyExistsChanged();
                }
            };
            await trackedDownloadService.TryStartDownload(download);

            await Task.Delay(Random.Shared.Next(50, 100));

            file.IsSelected = false;
        }
    }

    public override string Header => Resources.Label_HuggingFace;
}
