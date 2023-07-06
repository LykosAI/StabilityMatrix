using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Helper;
using StabilityMatrix.Models.Api;

namespace StabilityMatrix.ViewModels;

public partial class SelectModelVersionDialogViewModel : ObservableObject
{
    [ObservableProperty] private CivitModel civitModel;
    [ObservableProperty] private BitmapImage previewImage;
    [ObservableProperty] private ObservableCollection<CivitModelVersion> versions;
    [ObservableProperty] private CivitModelVersion selectedVersion;
    [ObservableProperty] private CivitFile selectedFile;

    public SelectModelVersionDialogViewModel(CivitModel civitModel)
    {
        CivitModel = civitModel;
        Versions = new ObservableCollection<CivitModelVersion>(CivitModel.ModelVersions);
        SelectedVersion = Versions.First();
        
        var firstImageUrl = Versions.FirstOrDefault()?.Images?.FirstOrDefault()?.Url;
        PreviewImage = firstImageUrl != null
            ? new BitmapImage(new Uri(firstImageUrl))
            : new BitmapImage(
                new Uri("pack://application:,,,/StabilityMatrix;component/Assets/noimage.png"));
    }

    partial void OnSelectedVersionChanged(CivitModelVersion value)
    {
        var firstImageUrl = value.Images?.FirstOrDefault()?.Url;
        PreviewImage = firstImageUrl != null
            ? new BitmapImage(new Uri(firstImageUrl))
            : new BitmapImage(
                new Uri("pack://application:,,,/StabilityMatrix;component/Assets/noimage.png"));
    }

    
}
