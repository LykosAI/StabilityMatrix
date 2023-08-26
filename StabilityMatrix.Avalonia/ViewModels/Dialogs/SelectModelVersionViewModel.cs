using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class SelectModelVersionViewModel : ContentDialogViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly IDownloadService downloadService;

    public required ContentDialog Dialog { get; set; }
    public required IReadOnlyList<ModelVersionViewModel> Versions { get; set; }
    public required string Description { get; set; }
    public required string Title { get; set; }

    [ObservableProperty] private Bitmap? previewImage;
    [ObservableProperty] private ModelVersionViewModel? selectedVersionViewModel;
    [ObservableProperty] private CivitFileViewModel? selectedFile;
    [ObservableProperty] private bool isImportEnabled;
    [ObservableProperty] private ObservableCollection<ImageSource> imageUrls = new();
    [ObservableProperty] private bool canGoToNextImage;
    [ObservableProperty] private bool canGoToPreviousImage;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayedPageNumber))]
    private int selectedImageIndex;

    public int DisplayedPageNumber => SelectedImageIndex + 1;

    public SelectModelVersionViewModel(ISettingsManager settingsManager,
        IDownloadService downloadService)
    {
        this.settingsManager = settingsManager;
        this.downloadService = downloadService;
    }

    public override void OnLoaded()
    {
        SelectedVersionViewModel = Versions[0];
        CanGoToNextImage = true;
    }

    partial void OnSelectedVersionViewModelChanged(ModelVersionViewModel? value)
    {
        var nsfwEnabled = settingsManager.Settings.ModelBrowserNsfwEnabled;
        var allImages = value?.ModelVersion?.Images?.Where(
            img => nsfwEnabled || img.Nsfw == "None")?.Select(x => new ImageSource(x.Url)).ToList();

        if (allImages == null || !allImages.Any())
        {
            allImages = new List<ImageSource> {new(Assets.NoImage)};
            CanGoToNextImage = false;
        }
        else
        {
            CanGoToNextImage = allImages.Count > 1;
        }

        Dispatcher.UIThread.Post(() =>
        {
            CanGoToPreviousImage = false;
            SelectedFile = SelectedVersionViewModel?.CivitFileViewModels.FirstOrDefault();
            ImageUrls = new ObservableCollection<ImageSource>(allImages);
            SelectedImageIndex = 0;
        });
    }

    partial void OnSelectedFileChanged(CivitFileViewModel? value)
    {
        IsImportEnabled = value?.CivitFile != null;
    }

    public void Cancel()
    {
        Dialog.Hide(ContentDialogResult.Secondary);
    }

    public void Import()
    {
        Dialog.Hide(ContentDialogResult.Primary);
    }

    public void PreviousImage()
    {
        if (SelectedImageIndex > 0) SelectedImageIndex--;
        CanGoToPreviousImage = SelectedImageIndex > 0;
        CanGoToNextImage = SelectedImageIndex < ImageUrls.Count - 1;
    }

    public void NextImage()
    {
        if (SelectedImageIndex < ImageUrls.Count - 1) SelectedImageIndex++;
        CanGoToPreviousImage = SelectedImageIndex > 0;
        CanGoToNextImage = SelectedImageIndex < ImageUrls.Count - 1;
    }
}
