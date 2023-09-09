using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ImageFolderCard))]
public partial class ImageFolderCardViewModel : ViewModelBase
{
    private readonly ILogger<ImageFolderCardViewModel> logger;
    private readonly IImageIndexService imageIndexService;
    private readonly ISettingsManager settingsManager;

    /// <summary>
    /// Source of image files to display
    /// </summary>
    private readonly SourceCache<LocalImageFile, string> localImagesSource =
        new(imageFile => imageFile.RelativePath);

    /// <summary>
    /// Collection of image items to display
    /// </summary>
    public IObservableCollection<ImageFolderCardItemViewModel> Items { get; } =
        new ObservableCollectionExtended<ImageFolderCardItemViewModel>();

    public ImageFolderCardViewModel(
        ILogger<ImageFolderCardViewModel> logger,
        IImageIndexService imageIndexService,
        ISettingsManager settingsManager
    )
    {
        this.logger = logger;
        this.imageIndexService = imageIndexService;
        this.settingsManager = settingsManager;

        var minDatetime = DateTimeOffset.FromUnixTimeMilliseconds(0);

        localImagesSource
            .Connect()
            .DeferUntilLoaded()
            .Transform(
                imageFile =>
                    new ImageFolderCardItemViewModel
                    {
                        LocalImageFile = imageFile,
                        ImagePath = Design.IsDesignMode
                            ? imageFile.RelativePath
                            : imageFile.GetFullPath(settingsManager.ImagesDirectory)
                    }
            )
            .SortBy(x => x.LocalImageFile?.LastModifiedAt ?? minDatetime, SortDirection.Descending)
            .Bind(Items)
            .Subscribe();
    }

    /// <inheritdoc />
    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        await imageIndexService.RefreshIndex("Inference");

        var imageFiles = await imageIndexService.GetLocalImagesByPrefix("Inference");

        localImagesSource.Edit(x =>
        {
            x.Load(imageFiles);
        });
    }
}
