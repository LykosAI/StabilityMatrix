using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.ViewModels;

public partial class SelectModelVersionDialogViewModel : ObservableObject
{
    private readonly ISettingsManager settingsManager;
    [ObservableProperty] private CivitModel civitModel;
    [ObservableProperty] private BitmapImage previewImage;
    [ObservableProperty] private ObservableCollection<CivitModelVersion> versions;
    [ObservableProperty] private CivitModelVersion selectedVersion;
    [ObservableProperty] private CivitFile selectedFile;
    [ObservableProperty] private bool isImportEnabled;

    public SelectModelVersionDialogViewModel(CivitModel civitModel, ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
        CivitModel = civitModel;
        Versions = new ObservableCollection<CivitModelVersion>(CivitModel.ModelVersions);
        SelectedVersion = Versions.First();

        var nsfwEnabled = settingsManager.Settings.ModelBrowserNsfwEnabled;
        var firstImageUrl = Versions.FirstOrDefault()?.Images
            ?.FirstOrDefault(img => nsfwEnabled || img.Nsfw == "None")?.Url;
        
        PreviewImage = firstImageUrl != null
            ? new BitmapImage(new Uri(firstImageUrl))
            : new BitmapImage(
                new Uri("pack://application:,,,/StabilityMatrix;component/Assets/noimage.png"));
    }

    partial void OnSelectedVersionChanged(CivitModelVersion value)
    {
        var nsfwEnabled = settingsManager.Settings.ModelBrowserNsfwEnabled;
        var firstImageUrl = value.Images?.FirstOrDefault(img => nsfwEnabled || img.Nsfw == "None")
            ?.Url;
        
        PreviewImage = firstImageUrl != null
            ? new BitmapImage(new Uri(firstImageUrl))
            : new BitmapImage(
                new Uri("pack://application:,,,/StabilityMatrix;component/Assets/noimage.png"));
    }

    partial void OnSelectedFileChanged(CivitFile value)
    {
        IsImportEnabled = value != null;
    }
    
    
}
