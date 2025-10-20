using System.Linq;
using Avalonia.Threading;
using StabilityMatrix.Avalonia.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

public interface IImageGalleryComponent
{
    ImageGalleryCardViewModel ImageGalleryCardViewModel { get; }

    /// <summary>
    /// Clears existing images and loads new ones
    /// </summary>
    public void LoadImagesToGallery(params ImageSource[] imageSources)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ImageGalleryCardViewModel.SelectedImage = null;
            ImageGalleryCardViewModel.SelectedImageIndex = -1;

            ImageGalleryCardViewModel.ImageSources.Clear();

            foreach (var imageSource in imageSources)
            {
                ImageGalleryCardViewModel.ImageSources.Add(imageSource);
            }

            ImageGalleryCardViewModel.SelectedImageIndex = imageSources.Length > 0 ? 0 : -1;
            ImageGalleryCardViewModel.SelectedImage = imageSources.FirstOrDefault();
        });
    }
}
