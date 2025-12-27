using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Avalonia.Helpers;

public static class EnumHelpers
{
    public static IEnumerable<CivitPeriod> AllCivitPeriods { get; } = Enum.GetValues<CivitPeriod>();

    public static IEnumerable<CivitSortMode> AllSortModes { get; } = Enum.GetValues<CivitSortMode>();

    public static IEnumerable<CivitModelType> AllCivitModelTypes { get; } =
        Enum.GetValues<CivitModelType>()
            .Where(t => t == CivitModelType.All || t.ConvertTo<SharedFolderType>() > 0)
            .OrderBy(t => t.ToString());

    public static IEnumerable<CivitModelType> MetadataEditorCivitModelTypes { get; } =
        Enum.GetValues<CivitModelType>().OrderBy(t => t.ToString());

    public static IEnumerable<CivitBaseModelType> AllCivitBaseModelTypes { get; } =
        Enum.GetValues<CivitBaseModelType>();

    public static IEnumerable<CivitBaseModelType> MetadataEditorCivitBaseModelTypes { get; } =
        AllCivitBaseModelTypes.Where(x => x != CivitBaseModelType.All);
}
