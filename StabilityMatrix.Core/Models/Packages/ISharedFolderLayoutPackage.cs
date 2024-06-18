using System.Collections.Immutable;

namespace StabilityMatrix.Core.Models.Packages;

public interface ISharedFolderLayoutPackage
{
    SharedFolderLayout SharedFolderLayout { get; }

    Dictionary<SharedFolderType, IReadOnlyList<string>> LegacySharedFolders
    {
        get
        {
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
                        if (!existingList.Contains(path))
                        {
                            result[folderTypeKey] = existingList.Add(path);
                        }
                    }
                }
            }

            return result;
        }
    }
}
