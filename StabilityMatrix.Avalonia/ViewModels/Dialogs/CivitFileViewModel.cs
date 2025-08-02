using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using NLog;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class CivitFileViewModel : DisposableViewModelBase
{
    private readonly IModelIndexService modelIndexService;
    private readonly ISettingsManager settingsManager;
    private readonly IServiceManager<ViewModelBase> vmFactory;
    private readonly Func<CivitFileViewModel, string?, Task>? downloadAction;

    [ObservableProperty]
    private CivitFile civitFile;

    [ObservableProperty]
    private bool isInstalled;

    [ObservableProperty]
    public required partial ObservableCollection<string> InstallLocations { get; set; }

    public CivitFileViewModel(
        IModelIndexService modelIndexService,
        ISettingsManager settingsManager,
        CivitFile civitFile,
        IServiceManager<ViewModelBase> vmFactory,
        Func<CivitFileViewModel, string?, Task>? downloadAction
    )
    {
        this.modelIndexService = modelIndexService;
        this.settingsManager = settingsManager;
        this.vmFactory = vmFactory;
        this.downloadAction = downloadAction;
        CivitFile = civitFile;
        IsInstalled =
            CivitFile is { Type: CivitFileType.Model, Hashes.BLAKE3: not null }
            && modelIndexService.ModelIndexBlake3Hashes.Contains(CivitFile.Hashes.BLAKE3);
        EventManager.Instance.ModelIndexChanged += ModelIndexChanged;
    }

    private void ModelIndexChanged(object? sender, EventArgs e)
    {
        IsInstalled =
            CivitFile is { Type: CivitFileType.Model, Hashes.BLAKE3: not null }
            && modelIndexService.ModelIndexBlake3Hashes.Contains(CivitFile.Hashes.BLAKE3);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteDownload))]
    private async Task DownloadToDefaultAsync()
    {
        if (downloadAction != null)
        {
            await downloadAction(this, null);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteDownload))]
    private async Task DownloadToSelectedLocationAsync(string locationKey)
    {
        if (downloadAction != null)
        {
            await downloadAction(this, locationKey);
        }
    }

    [RelayCommand]
    private async Task Delete()
    {
        var hash = CivitFile.Hashes.BLAKE3;
        if (string.IsNullOrWhiteSpace(hash))
        {
            return;
        }

        var matchingModels = (await modelIndexService.FindByHashAsync(hash)).ToList();

        if (matchingModels.Count == 0)
        {
            await modelIndexService.RefreshIndex();
            matchingModels = (await modelIndexService.FindByHashAsync(hash)).ToList();

            if (matchingModels.Count == 0)
            {
                return;
            }
        }

        var confirmDeleteVm = vmFactory.Get<ConfirmDeleteDialogViewModel>();
        var paths = new List<string>();

        foreach (var localModel in matchingModels)
        {
            var checkpointPath = new FilePath(localModel.GetFullPath(settingsManager.ModelsDirectory));
            if (File.Exists(checkpointPath))
            {
                paths.Add(checkpointPath);
            }

            var previewPath = localModel.GetPreviewImageFullPath(settingsManager.ModelsDirectory);
            if (File.Exists(previewPath))
            {
                paths.Add(previewPath);
            }

            var cmInfoPath = checkpointPath.ToString().Replace(checkpointPath.Extension, ".cm-info.json");
            if (File.Exists(cmInfoPath))
            {
                paths.Add(cmInfoPath);
            }
        }

        confirmDeleteVm.PathsToDelete = paths;

        if (await confirmDeleteVm.GetDialog().ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await confirmDeleteVm.ExecuteCurrentDeleteOperationAsync(failFast: true);
        }
        catch (Exception e)
        {
            LogManager
                .GetCurrentClassLogger()
                .Error(e, "Failed to delete model files for {ModelName}", CivitFile.Name);
            await modelIndexService.RefreshIndex();
            return;
        }
        finally
        {
            IsInstalled = false;
        }

        await modelIndexService.RemoveModelsAsync(matchingModels);
    }

    private bool CanExecuteDownload()
    {
        return downloadAction != null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            EventManager.Instance.ModelIndexChanged -= ModelIndexChanged;
        }
        base.Dispose(disposing);
    }
}
