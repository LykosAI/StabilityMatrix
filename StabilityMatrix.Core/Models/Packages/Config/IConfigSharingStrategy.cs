namespace StabilityMatrix.Core.Models.Packages.Config;

public interface IConfigSharingStrategy
{
    /// <summary>
    /// Reads the config stream, updates paths based on the layout and selector, and writes back to the stream.
    /// </summary>
    Task UpdateAndWriteAsync(
        Stream configStream,
        SharedFolderLayout layout,
        Func<SharedFolderLayoutRule, IEnumerable<string>> pathsSelector,
        IEnumerable<string> clearPaths,
        ConfigSharingOptions options,
        CancellationToken cancellationToken = default
    );
}
