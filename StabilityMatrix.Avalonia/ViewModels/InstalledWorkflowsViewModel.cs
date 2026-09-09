using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.OpenArt;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(InstalledWorkflowsPage))]
[RegisterSingleton<InstalledWorkflowsViewModel>]
public partial class InstalledWorkflowsViewModel(
    ISettingsManager settingsManager,
    INotificationService notificationService,
    IPackageFactory packageFactory,
    IServiceManager<ViewModelBase> vmFactory,
    INavigationService<MainWindowViewModel> navigationService
) : PageViewModelBase
{
    public override string Title => Resources.Label_Workflows;
    public override IconSource IconSource => new FASymbolIconSource { Symbol = "fa-solid fa-circle-nodes" };

    private readonly SourceCache<InstalledWorkflow, string> workflowsCache = new(x =>
        x.Workflow?.Id ?? Guid.NewGuid().ToString()
    );

    [ObservableProperty]
    private IObservableCollection<InstalledWorkflow> displayedWorkflows =
        new ObservableCollectionExtended<InstalledWorkflow>();

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFolders))]
    private ObservableCollection<WorkflowCategory> categories = [];

    [ObservableProperty]
    private WorkflowCategory? selectedCategory;

    [ObservableProperty]
    private bool showWorkflowsInSubfolders = true;

    public bool HasFolders =>
        Categories.Any(c => c.SubDirectories.Count > 0 || c.FilterPath is { Length: > 0 });

    protected override async Task OnInitialLoadedAsync()
    {
        await base.OnInitialLoadedAsync();

        var filterPredicate = this.WhenPropertyChanged(vm => vm.SearchQuery)
            .Throttle(TimeSpan.FromMilliseconds(100))
            .DistinctUntilChanged()
            .Select(_ => Unit.Default)
            .Merge(this.WhenPropertyChanged(vm => vm.SelectedCategory).Select(_ => Unit.Default))
            .Merge(this.WhenPropertyChanged(vm => vm.ShowWorkflowsInSubfolders).Select(_ => Unit.Default))
            .Select(_ => (Func<InstalledWorkflow, bool>)FilterWorkflows);

        AddDisposable(
            workflowsCache
                .Connect()
                .DeferUntilLoaded()
                .Filter(filterPredicate)
                .SortBy(x => x.Index)
                .Bind(DisplayedWorkflows)
                .ObserveOn(SynchronizationContext.Current!)
                .Subscribe()
        );

        AddDisposable(
            settingsManager.RelayPropertyFor(
                this,
                vm => vm.ShowWorkflowsInSubfolders,
                settings => settings.ShowWorkflowsInSubfolders,
                true
            )
        );

        if (Design.IsDesignMode)
            return;

        await LoadInstalledWorkflowsAsync();
        EventManager.Instance.WorkflowInstalled += OnWorkflowInstalled;
    }

    [RelayCommand]
    private async Task LoadInstalledWorkflowsAsync()
    {
        workflowsCache.Clear();

        if (!Directory.Exists(settingsManager.WorkflowDirectory))
        {
            Directory.CreateDirectory(settingsManager.WorkflowDirectory);
        }

        var count = 0;

        foreach (
            var workflowPath in LinkSafeFileSystem.EnumerateFiles(settingsManager.WorkflowDirectory, "*.json")
        )
        {
            try
            {
                var json = await File.ReadAllTextAsync(workflowPath);
                var metadata = JsonSerializer.Deserialize<InstalledWorkflow>(json);

                if (metadata?.Workflow == null)
                {
                    metadata = new InstalledWorkflow
                    {
                        Workflow = new OpenArtSearchResult
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = Path.GetFileNameWithoutExtension(workflowPath),
                        },
                    };
                }

                metadata.Index = count++;
                metadata.Folder = GetRelativeFolder(workflowPath);
                metadata.LocalPreviewPath = FindPreviewSidecar(workflowPath);
                metadata.FilePath = [await App.StorageProvider.TryGetFileFromPathAsync(workflowPath)];
                workflowsCache.AddOrUpdate(metadata);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        RefreshCategories();
    }

    private static string? FindPreviewSidecar(string workflowPath)
    {
        var directory = Path.GetDirectoryName(workflowPath);
        if (directory is null)
            return null;

        return Directory
            .EnumerateFiles(directory, $"{Path.GetFileNameWithoutExtension(workflowPath)}.preview.*")
            .FirstOrDefault();
    }

    /// <summary>
    /// Folder of the file relative to the workflow library root, using '/' separators;
    /// empty for files directly in the root.
    /// </summary>
    private string GetRelativeFolder(string workflowPath)
    {
        var relative = Path.GetRelativePath(settingsManager.WorkflowDirectory, workflowPath);
        var folder = Path.GetDirectoryName(relative);

        return folder is null or "." or ""
            ? string.Empty
            : folder.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private void RefreshCategories()
    {
        var workflows = workflowsCache.Items.ToList();
        var libraryRoot = settingsManager.WorkflowDirectory;

        int CountSubtree(string folderPath) =>
            workflows.Count(x =>
                x.Folder.Equals(folderPath, StringComparison.OrdinalIgnoreCase)
                || x.Folder.StartsWith($"{folderPath}/", StringComparison.OrdinalIgnoreCase)
            );

        var allNode = new WorkflowCategory
        {
            Name = Resources.Label_AllWorkflows,
            Path = libraryRoot,
            FilterPath = null,
            Count = workflows.Count,
            IsExpanded = true,
        };

        var nodesByPath = new Dictionary<string, WorkflowCategory>(StringComparer.OrdinalIgnoreCase);
        var rootNodes = new List<WorkflowCategory>();

        foreach (
            var folderPath in workflows
                .Select(x => x.Folder)
                .Where(f => f.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
        )
        {
            // Materialize every level of the path so intermediate folders become nodes
            var segments = folderPath.Split('/');
            for (var depth = 1; depth <= segments.Length; depth++)
            {
                var path = string.Join('/', segments.Take(depth));
                if (nodesByPath.ContainsKey(path))
                    continue;

                var node = new WorkflowCategory
                {
                    Name = segments[depth - 1],
                    Path = Path.Combine(libraryRoot, path.Replace('/', Path.DirectorySeparatorChar)),
                    FilterPath = path,
                    Count = CountSubtree(path),
                };
                nodesByPath[path] = node;

                if (depth == 1)
                {
                    rootNodes.Add(node);
                }
                else
                {
                    nodesByPath[string.Join('/', segments.Take(depth - 1))].SubDirectories.Add(node);
                }
            }
        }

        var entries = new List<WorkflowCategory> { allNode };

        var rootFileCount = workflows.Count(x => x.Folder.Length == 0);
        if (rootNodes.Count > 0 && rootFileCount > 0)
        {
            entries.Add(
                new WorkflowCategory
                {
                    Name = Resources.Label_Ungrouped,
                    Path = libraryRoot,
                    FilterPath = string.Empty,
                    Count = rootFileCount,
                }
            );
        }

        entries.AddRange(rootNodes);

        var previousFilter = SelectedCategory?.FilterPath;
        Categories = new ObservableCollection<WorkflowCategory>(entries);
        SelectedCategory =
            (
                previousFilter is null
                    ? null
                    : entries
                        .SelectMany(Flatten)
                        .FirstOrDefault(c =>
                            string.Equals(c.FilterPath, previousFilter, StringComparison.OrdinalIgnoreCase)
                        )
            ) ?? allNode;

        static IEnumerable<WorkflowCategory> Flatten(WorkflowCategory node) =>
            new[] { node }.Concat(node.SubDirectories.SelectMany(Flatten));
    }

    [RelayCommand]
    private async Task OpenInExplorer(InstalledWorkflow workflow)
    {
        if (workflow.FilePath == null)
            return;

        var path = workflow.FilePath.FirstOrDefault()?.Path.ToString();
        if (string.IsNullOrWhiteSpace(path))
            return;

        await ProcessRunner.OpenFileBrowser(path);
    }

    [RelayCommand]
    private void OpenSourcePage(InstalledWorkflow workflow)
    {
        if (workflow.SourceUrl is { } url && !string.IsNullOrWhiteSpace(url))
        {
            ProcessRunner.OpenUrl(url);
        }
    }

    /// <summary>
    /// Opens the CivitAI details page for workflows imported from there;
    /// no-op for files without a resolvable source.
    /// </summary>
    [RelayCommand]
    private void OpenDetails(InstalledWorkflow workflow)
    {
        if (!CivitaiUrlHelper.TryParseModelId(workflow.SourceUrl, out var modelId))
            return;

        var detailsVm = vmFactory.Get<CheckpointBrowser.CivitDetailsPageViewModel>(vm =>
        {
            vm.CivitModel = new CivitModel { Id = modelId };
            vm.ModelIdList = [modelId];
            vm.CurrentIndex = 0;
            return vm;
        });

        navigationService.NavigateTo(detailsVm, BetterSlideNavigationTransition.PageSlideFromRight);
    }

    [RelayCommand]
    private async Task OpenFolderInExplorer(WorkflowCategory? category)
    {
        if (category?.Path is { Length: > 0 } path && Directory.Exists(path))
        {
            await ProcessRunner.OpenFolderBrowser(path);
        }
    }

    private static bool CanDeleteFolder(WorkflowCategory? category) =>
        category?.FilterPath is { Length: > 0 };

    [RelayCommand(CanExecute = nameof(CanDeleteFolder))]
    private async Task DeleteFolderAsync(WorkflowCategory? category)
    {
        if (
            category?.FilterPath is not { Length: > 0 }
            || category.Path is not { Length: > 0 } folderPath
            || !Directory.Exists(folderPath)
        )
        {
            return;
        }

        var confirmationDialog = new BetterContentDialog
        {
            Title = Resources.Label_AreYouSure,
            Content = $"{folderPath}\n{Resources.Label_ActionCannotBeUndone}",
            PrimaryButtonText = Resources.Action_Delete,
            SecondaryButtonText = Resources.Action_Cancel,
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = true,
        };

        if (await confirmationDialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        await notificationService.TryAsync(
            Task.Run(() => Directory.Delete(folderPath, true)),
            message: "Error deleting folder"
        );

        // Reloads the library and invalidates the model browser's installed detection
        EventManager.Instance.OnWorkflowInstalled();
    }

    [RelayCommand]
    private async Task EditMetadataAsync(InstalledWorkflow workflow)
    {
        var vm = new WorkflowMetadataEditorViewModel { Workflow = workflow };

        var dialog = new BetterContentDialog
        {
            IsPrimaryButtonEnabled = true,
            IsSecondaryButtonEnabled = true,
            PrimaryButtonText = Resources.Action_Save,
            SecondaryButtonText = Resources.Action_Cancel,
            DefaultButton = ContentDialogButton.Primary,
            IsFooterVisible = true,
            MaxDialogWidth = 560,
            CloseOnClickOutside = true,
            Content = vm,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        await notificationService.TryAsync(vm.SaveAsync(), message: "Error saving workflow metadata");
        await LoadInstalledWorkflowsAsync();
    }

    [RelayCommand]
    private async Task CheckRequiredNodesAsync(InstalledWorkflow workflow)
    {
        if (workflow.FilePath?.FirstOrDefault()?.TryGetLocalPath() is not { } path)
            return;

        await WorkflowNodesDialogViewModel.ShowDialogAsync(
            settingsManager,
            packageFactory,
            [new FilePath(path)]
        );
    }

    [RelayCommand]
    private async Task DeleteAsync(InstalledWorkflow workflow)
    {
        var confirmationDialog = new BetterContentDialog
        {
            Title = Resources.Label_AreYouSure,
            Content = Resources.Label_ActionCannotBeUndone,
            PrimaryButtonText = Resources.Action_Delete,
            SecondaryButtonText = Resources.Action_Cancel,
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = true,
        };
        var dialogResult = await confirmationDialog.ShowAsync();
        if (dialogResult != ContentDialogResult.Primary)
            return;

        await using var delay = new MinimumDelay(200, 500);

        var path = workflow?.FilePath?.FirstOrDefault()?.Path.ToString().Replace("file:///", "");
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            await notificationService.TryAsync(
                Task.Run(() => File.Delete(path)),
                message: "Error deleting workflow"
            );

            var id = workflow?.Workflow?.Id;
            if (!string.IsNullOrWhiteSpace(id))
            {
                workflowsCache.Remove(id);
            }
        }

        notificationService.Show(
            Resources.Label_WorkflowDeleted,
            string.Format(Resources.Label_WorkflowDeletedSuccessfully, workflow?.Workflow?.Name)
        );
    }

    private bool FilterWorkflows(InstalledWorkflow workflow)
    {
        bool isCategoryMatch;
        if (SelectedCategory?.FilterPath is not { } filterPath)
        {
            // "All Workflows" - with nested display off, only library-root files
            isCategoryMatch = ShowWorkflowsInSubfolders || workflow.Folder.Length == 0;
        }
        else if (filterPath.Length == 0)
        {
            // "Ungrouped" - always library-root files only
            isCategoryMatch = workflow.Folder.Length == 0;
        }
        else
        {
            isCategoryMatch =
                workflow.Folder.Equals(filterPath, StringComparison.OrdinalIgnoreCase)
                || (
                    ShowWorkflowsInSubfolders
                    && workflow.Folder.StartsWith($"{filterPath}/", StringComparison.OrdinalIgnoreCase)
                );
        }

        if (!isCategoryMatch)
            return false;

        if (string.IsNullOrWhiteSpace(SearchQuery))
            return true;

        if (workflow.HasMetadata)
        {
            return workflow.Workflow.Creator.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)
                || workflow.Workflow.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);
        }

        return workflow.Workflow?.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private void OnWorkflowInstalled(object? sender, EventArgs e)
    {
        LoadInstalledWorkflowsAsync().SafeFireAndForget();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            EventManager.Instance.WorkflowInstalled -= OnWorkflowInstalled;
        }

        base.Dispose(disposing);
    }
}
