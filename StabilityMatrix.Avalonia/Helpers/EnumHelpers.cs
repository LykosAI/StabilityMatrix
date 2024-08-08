using System;
using System.Collections.Generic;
using System.Linq;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Avalonia.Helpers;

public static class EnumHelpers
{
    public static IEnumerable<CivitPeriod> AllCivitPeriods { get; } =
        Enum.GetValues(typeof(CivitPeriod)).Cast<CivitPeriod>();

    public static IEnumerable<CivitSortMode> AllSortModes { get; } =
        Enum.GetValues(typeof(CivitSortMode)).Cast<CivitSortMode>();

    public static IEnumerable<CivitModelType> AllCivitModelTypes { get; } =
        Enum.GetValues(typeof(CivitModelType))
            .Cast<CivitModelType>()
            .Where(t => t == CivitModelType.All || t.ConvertTo<SharedFolderType>() > 0)
            .OrderBy(t => t.ToString());

    public static IEnumerable<CivitModelType> MetadataEditorCivitModelTypes { get; } =
        Enum.GetValues(typeof(CivitModelType)).Cast<CivitModelType>().OrderBy(t => t.ToString());

    public static IEnumerable<CivitBaseModelType> AllCivitBaseModelTypes { get; } =
        Enum.GetValues(typeof(CivitBaseModelType)).Cast<CivitBaseModelType>();

    public static IEnumerable<CivitBaseModelType> MetadataEditorCivitBaseModelTypes { get; } =
        AllCivitBaseModelTypes.Where(x => x != CivitBaseModelType.All);
}
