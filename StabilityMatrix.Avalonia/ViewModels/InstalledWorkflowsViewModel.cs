using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Xaml.Interactions.DragAndDrop;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.OpenArt;
using StabilityMatrix.Core.Services;
using Windows.Storage;
using IStorageFile = Avalonia.Platform.Storage.IStorageFile;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(InstalledWorkflowsPage))]
[Singleton]
public partial class InstalledWorkflowsViewModel(ISettingsManager settingsManager) : TabViewModelBase
{
    public override string Header => "Installed Workflows";

    private readonly SourceCache<OpenArtMetadata, string> workflowsCache = new(x => x.Id);

    [ObservableProperty]
    private IObservableCollection<OpenArtMetadata> displayedWorkflows =
        new ObservableCollectionExtended<OpenArtMetadata>();

    protected override async Task OnInitialLoadedAsync()
    {
        await base.OnInitialLoadedAsync();

        workflowsCache.Connect().DeferUntilLoaded().Bind(DisplayedWorkflows).Subscribe();

        if (Design.IsDesignMode)
            return;

        await LoadInstalledWorkflowsAsync();
    }

    [RelayCommand]
    private async Task LoadInstalledWorkflowsAsync()
    {
        workflowsCache.Clear();

        foreach (
            var workflowPath in Directory.EnumerateFiles(
                settingsManager.WorkflowDirectory,
                "*.json",
                SearchOption.AllDirectories
            )
        )
        {
            try
            {
                var json = await File.ReadAllTextAsync(workflowPath);
                var metadata = JsonSerializer.Deserialize<OpenArtMetadata>(json);

                if (metadata?.Id == null)
                {
                    metadata = new OpenArtMetadata
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = Path.GetFileNameWithoutExtension(workflowPath)
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
}
