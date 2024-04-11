using System.IO;
using System.Threading.Tasks;

namespace StabilityMatrix.Avalonia.Controls.VendorLabs.Cache;

/// <summary>
/// Provides methods and tools to cache files in a folder
/// </summary>
internal class FileCache : CacheBase<string>
{
    /// <summary>
    /// Private singleton field.
    /// </summary>
    private static FileCache? _instance;

    /// <summary>
    /// Gets public singleton property.
    /// </summary>
    public static FileCache Instance => _instance ?? (_instance = new FileCache());

    protected override Task<string> ConvertFromAsync(Stream stream)
    {
        // nothing to do in this instance;
        return Task.FromResult<string>("");
    }

    /// <summary>
    /// Returns a cached path
    /// </summary>
    /// <param name="baseFile">storage file</param>
    /// <returns>awaitable task</returns>
    protected override Task<string> ConvertFromAsync(string baseFile)
    {
        return Task.FromResult(baseFile);
    }
}
