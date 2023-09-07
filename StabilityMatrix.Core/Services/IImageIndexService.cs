using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Core.Services;

public interface IImageIndexService
{
    /// <summary>
    /// Gets a list of local images that start with the given path prefix
    /// </summary>
    Task<IReadOnlyList<LocalImageFile>> GetLocalImagesByPrefix(string pathPrefix);

    /// <summary>
    /// Refreshes the index of local images
    /// </summary>
    Task RefreshIndex(string subPath = "");

    /// <summary>
    /// Refreshes the index of local images in the background
    /// </summary>
    void BackgroundRefreshIndex();
}
