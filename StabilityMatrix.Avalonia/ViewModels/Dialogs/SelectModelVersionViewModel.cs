using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class SelectModelVersionViewModel : ContentDialogViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly IDownloadService downloadService;
    
    public required ContentDialog Dialog { get; set; }
    public required IReadOnlyList<CivitModelVersion> Versions { get; set; }
    
    [ObservableProperty] private Bitmap? previewImage;
    [ObservableProperty] private CivitModelVersion? selectedVersion;
    [ObservableProperty] private CivitFile? selectedFile;
    [ObservableProperty] private bool isImportEnabled;
    
    public SelectModelVersionViewModel(ISettingsManager settingsManager, IDownloadService downloadService)
    {
        this.settingsManager = settingsManager;
        this.downloadService = downloadService;
    }
    
    public override void OnLoaded()
    {
        SelectedVersion = Versions[0];
    }
    
    partial void OnSelectedVersionChanged(CivitModelVersion? value)
    {
        var nsfwEnabled = settingsManager.Settings.ModelBrowserNsfwEnabled;
        var firstImageUrl = value?.Images?.FirstOrDefault(
                img => nsfwEnabled || img.Nsfw == "None")?.Url;

        Dispatcher.UIThread.InvokeAsync(async 
            () => await UpdateImage(firstImageUrl));
    }
    
    partial void OnSelectedFileChanged(CivitFile? value)
    {
        IsImportEnabled = value != null;
    }

    private async Task UpdateImage(string? url = null)
    {
        var assetStream = string.IsNullOrWhiteSpace(url)
            ? AssetLoader.Open(new Uri("avares://StabilityMatrix.Avalonia/Assets/noimage.png"))
            : await downloadService.GetImageStreamFromUrl(url);
        
        PreviewImage = new Bitmap(assetStream);
    }

    public void Cancel()
    {
        Dialog.Hide(ContentDialogResult.Secondary);
    }

    public void Import()
    {
        Dialog.Hide(ContentDialogResult.Primary);
    }
}
