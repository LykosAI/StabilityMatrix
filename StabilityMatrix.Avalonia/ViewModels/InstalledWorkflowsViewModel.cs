using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api.OpenArt;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(InstalledWorkflowsPage))]
[Singleton]
public partial class InstalledWorkflowsViewModel(
    ISettingsManager settingsManager,
    INotificationService notificationService
) : TabViewModelBase, IDisposable
{
    public override string Header => Resources.TabLabel_InstalledWorkflows;

    private readonly SourceCache<OpenArtMetadata, string> workflowsCache =
        new(x => x.Workflow?.Id ?? Guid.NewGuid().ToString());

    [ObservableProperty]
    private IObservableCollection<OpenArtMetadata> displayedWorkflows =
        new ObservableCollectionExtended<OpenArtMetadata>();

    protected override async Task OnInitialLoadedAsync()
    {
        await base.OnInitialLoadedAsync();

        workflowsCache.Connect().DeferUntilLoaded().Bind(DisplayedWorkflows).ObserveOn(SynchronizationContext.Current).Subscribe();

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
                var metadata = JsonSerializer.Deserialize<OpenArtMetadata>(json);

                if (metadata?.Workflow == null)
                {
                    metadata = new OpenArtMetadata
                    {
                        Workflow = new OpenArtSearchResult
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = Path.GetFileNameWithoutExtension(workflowPath)
                        }
                    };
                }

                metadata.FilePath = [await App.StorageProvider.TryGetFileFromPathAsync(workflowPath)];
                workflowsCache.AddOrUpdate(metadata);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    [RelayCommand]
    private async Task OpenInExplorer(OpenArtMetadata metadata)
    {
        if (metadata.FilePath == null)
            return;

        var path = metadata.FilePath.FirstOrDefault()?.Path.ToString();
        if (string.IsNullOrWhiteSpace(path))
            return;

        await ProcessRunner.OpenFileBrowser(path);
    }

    [RelayCommand]
    private void OpenOnOpenArt(OpenArtMetadata metadata)
    {
        if (metadata.Workflow == null)
            return;

        ProcessRunner.OpenUrl($"https://openart.ai/workflows/{metadata.Workflow.Id}");
    }

    [RelayCommand]
    private async Task DeleteAsync(OpenArtMetadata metadata)
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

        var path = metadata?.FilePath?.FirstOrDefault()?.Path.ToString().Replace("file:///", "");
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            await notificationService.TryAsync(
                Task.Run(() => File.Delete(path)),
                message: "Error deleting workflow"
            );

            var id = metadata?.Workflow?.Id;
            if (!string.IsNullOrWhiteSpace(id))
            {
                workflowsCache.Remove(id);
            }
        }

        notificationService.Show(
            Resources.Label_WorkflowDeleted,
            string.Format(Resources.Label_WorkflowDeletedSuccessfully, metadata?.Workflow?.Name)
        );
    }

    private void OnWorkflowInstalled(object? sender, EventArgs e)
    {
        LoadInstalledWorkflowsAsync().SafeFireAndForget();
    }

    public void Dispose()
    {
        workflowsCache.Dispose();
        EventManager.Instance.WorkflowInstalled -= OnWorkflowInstalled;
    }
}
