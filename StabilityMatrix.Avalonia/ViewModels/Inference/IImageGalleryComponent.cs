using System.Linq;
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
        ImageGalleryCardViewModel.ImageSources.Clear();

        foreach (var imageSource in imageSources)
        {
            ImageGalleryCardViewModel.ImageSources.Add(imageSource);
        }

        ImageGalleryCardViewModel.SelectedImage = imageSources.FirstOrDefault();
    }
}
