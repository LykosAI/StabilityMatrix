using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class SelectModelVersionViewModel : ObservableObject
{
    private readonly ContentDialog dialog;
    private readonly ISettingsManager settingsManager;
    private readonly IDownloadService downloadService;
    [ObservableProperty] private CivitModel civitModel;
    [ObservableProperty] private Bitmap previewImage;
    [ObservableProperty] private ObservableCollection<CivitModelVersion> versions;
    [ObservableProperty] private CivitModelVersion selectedVersion;
    [ObservableProperty] private CivitFile selectedFile;
    [ObservableProperty] private bool isImportEnabled;
    
    public SelectModelVersionViewModel(CivitModel civitModel, ContentDialog dialog, ISettingsManager settingsManager, IDownloadService downloadService)
    {
        this.dialog = dialog;
        this.settingsManager = settingsManager;
        this.downloadService = downloadService;

        CivitModel = civitModel;
        Versions = new ObservableCollection<CivitModelVersion>(CivitModel.ModelVersions);
        SelectedVersion = Versions.First();

        var nsfwEnabled = settingsManager.Settings.ModelBrowserNsfwEnabled;
        var firstImageUrl = Versions.FirstOrDefault()?.Images
            ?.FirstOrDefault(img => nsfwEnabled || img.Nsfw == "None")?.Url;
        
        UpdateImage(firstImageUrl).SafeFireAndForget();
    }
    
    partial void OnSelectedVersionChanged(CivitModelVersion value)
    {
        var nsfwEnabled = settingsManager.Settings.ModelBrowserNsfwEnabled;
        var firstImageUrl = value.Images?.FirstOrDefault(img => nsfwEnabled || img.Nsfw == "None")
            ?.Url;

        UpdateImage(firstImageUrl).SafeFireAndForget();
    }
    
    partial void OnSelectedFileChanged(CivitFile value)
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
        dialog.Hide(ContentDialogResult.Secondary);
    }

    public void Import()
    {
        dialog.Hide(ContentDialogResult.Primary);
    }
}
