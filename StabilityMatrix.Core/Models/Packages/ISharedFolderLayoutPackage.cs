using System.Collections.Immutable;

namespace StabilityMatrix.Core.Models.Packages;

public interface ISharedFolderLayoutPackage
{
    SharedFolderLayout SharedFolderLayout { get; }

    Dictionary<SharedFolderType, IReadOnlyList<string>> LegacySharedFolders
    {
        get
        {
            // Keep track of unique paths since symbolic links can't do multiple targets
            // So we'll ignore duplicates once they appear here
            var addedPaths = new HashSet<string>();
            var result = new Dictionary<SharedFolderType, IReadOnlyList<string>>();

            foreach (var rule in SharedFolderLayout.Rules)
            {
                if (rule.TargetRelativePaths is not { Length: > 0 } value)
                {
                    continue;
                }

                foreach (var folderTypeKey in rule.SourceTypes)
                {
                    var existingList =
                        (ImmutableList<string>)
                            result.GetValueOrDefault(folderTypeKey, ImmutableList<string>.Empty);

                    foreach (var path in value)
                    {
                        // Skip if the path is already in the list
                        if (existingList.Contains(path))
                            continue;

                        // Skip if the path is already added globally
                        if (!addedPaths.Add(path))
                            continue;

                        result[folderTypeKey] = existingList.Add(path);
                    }
                }
            }

            return result;
        }
    }
}
