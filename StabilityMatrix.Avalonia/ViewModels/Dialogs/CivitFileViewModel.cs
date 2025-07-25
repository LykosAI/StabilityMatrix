using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
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
        Func<CivitFileViewModel, string?, Task>? downloadAction
    )
    {
        this.modelIndexService = modelIndexService;
        this.settingsManager = settingsManager;
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

        var dialog = new BetterContentDialog
        {
            Title = Resources.Label_AreYouSure,
            MaxDialogWidth = 750,
            MaxDialogHeight = 850,
            PrimaryButtonText = Resources.Action_Yes,
            IsPrimaryButtonEnabled = true,
            IsSecondaryButtonEnabled = false,
            CloseButtonText = Resources.Action_Cancel,
            DefaultButton = ContentDialogButton.Close,
            Content =
                $"The following files:\n{string.Join('\n', matchingModels.Select(x => $"- {x.FileName}"))}\n"
                + "\nand all associated metadata files will be deleted. Are you sure?",
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            foreach (var localModel in matchingModels)
            {
                var checkpointPath = new FilePath(localModel.GetFullPath(settingsManager.ModelsDirectory));
                if (File.Exists(checkpointPath))
                {
                    File.Delete(checkpointPath);
                }

                var previewPath = localModel.GetPreviewImageFullPath(settingsManager.ModelsDirectory);
                if (File.Exists(previewPath))
                {
                    File.Delete(previewPath);
                }

                var cmInfoPath = checkpointPath.ToString().Replace(checkpointPath.Extension, ".cm-info.json");
                if (File.Exists(cmInfoPath))
                {
                    File.Delete(cmInfoPath);
                }

                await modelIndexService.RemoveModelAsync(localModel);
                IsInstalled = false;
            }
        }
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
