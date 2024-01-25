using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class RecommendedModelItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private string author;

    [ObservableProperty]
    private CivitModelVersion modelVersion;

    [ObservableProperty]
    private CivitModel civitModel;

    public Uri ThumbnailUrl =>
        ModelVersion.Images?.FirstOrDefault()?.Url == null
            ? Assets.NoImage
            : new Uri(ModelVersion.Images.First().Url);
}
