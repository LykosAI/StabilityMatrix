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
    IPackageFactory packageFactory
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
    private ObservableCollection<WorkflowFolder> folders = [];

    [ObservableProperty]
    private WorkflowFolder? selectedFolder;

    public bool HasFolders => Folders.Count > 1;

    protected override async Task OnInitialLoadedAsync()
    {
        await base.OnInitialLoadedAsync();

        var filterPredicate = this.WhenPropertyChanged(vm => vm.SearchQuery)
            .Throttle(TimeSpan.FromMilliseconds(100))
            .DistinctUntilChanged()
            .Select(_ => Unit.Default)
            .Merge(this.WhenPropertyChanged(vm => vm.SelectedFolder).Select(_ => Unit.Default))
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
            var workflowPath in Directory.EnumerateFiles(
                settingsManager.WorkflowDirectory,
                "*.json",
                EnumerationOptionConstants.AllDirectories
            )
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
                metadata.Folder = GetTopLevelFolder(workflowPath);
                metadata.LocalPreviewPath = FindPreviewSidecar(workflowPath);
                metadata.FilePath = [await App.StorageProvider.TryGetFileFromPathAsync(workflowPath)];
                workflowsCache.AddOrUpdate(metadata);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        RefreshFolders();
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

    private string GetTopLevelFolder(string workflowPath)
    {
        var relative = Path.GetRelativePath(settingsManager.WorkflowDirectory, workflowPath);
        var folder = Path.GetDirectoryName(relative)
            ?.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .FirstOrDefault();

        return folder is null or "." ? string.Empty : folder;
    }

    private void RefreshFolders()
    {
        var folderNames = workflowsCache
            .Items.Select(x => x.Folder)
            .Where(f => f.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var entries = new List<WorkflowFolder> { new(null, Resources.Label_AllWorkflows) };

        if (folderNames.Count > 0 && workflowsCache.Items.Any(x => x.Folder.Length == 0))
        {
            entries.Add(new WorkflowFolder(string.Empty, Resources.Label_Ungrouped));
        }

        entries.AddRange(folderNames.Select(f => new WorkflowFolder(f, f)));

        var previousKey = SelectedFolder?.Key;
        Folders = new ObservableCollection<WorkflowFolder>(entries);
        SelectedFolder = entries.FirstOrDefault(e => e.Key == previousKey) ?? entries[0];
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
        if (
            SelectedFolder is { Key: { } folderKey }
            && !workflow.Folder.Equals(folderKey, StringComparison.OrdinalIgnoreCase)
        )
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
