using System;
using System.Collections.Generic;

namespace StabilityMatrix.Core.Models.Api.CivArchive;

public enum CivArchiveSortOption
{
    Newest,
    Oldest,
    Top,
    Relevance,
    DeletedNewest,
    DeletedOldest,
}

public enum CivArchiveKindOption
{
    All,
    Version,
    User,
    File,
}

public enum CivArchivePeriodOption
{
    Week,
    Month,
    Quarter,
    Half,
    Year,
    All,
}

public enum CivArchiveRatingOption
{
    All,
    Explicit,
    Safe,
}

public enum CivArchivePlatformStatusOption
{
    All,
    Available,
    Deleted,
}

public enum CivArchivePlatformOption
{
    All,
    Civitai,
    Tensorart,
    Tensorhub,
    Seaart,
    Civision,
    Pixai,
    Tungsten,
    Yodayo,
    Moescape,
    Shakker,
    Huggingface,
    Modelscope,
    ModelscopeCn,
}

public static class CivArchiveEnumExtensions
{
    private static readonly IReadOnlyDictionary<CivArchiveSortOption, string> SortMap = new Dictionary<
        CivArchiveSortOption,
        string
    >
    {
        [CivArchiveSortOption.Newest] = "newest",
        [CivArchiveSortOption.Oldest] = "oldest",
        [CivArchiveSortOption.Top] = "top",
        [CivArchiveSortOption.Relevance] = "relevance",
        [CivArchiveSortOption.DeletedNewest] = "deleted_newest",
        [CivArchiveSortOption.DeletedOldest] = "deleted_oldest",
    };

    private static readonly IReadOnlyDictionary<CivArchiveKindOption, string> KindMap = new Dictionary<
        CivArchiveKindOption,
        string
    >
    {
        [CivArchiveKindOption.All] = "all",
        [CivArchiveKindOption.Version] = "version",
        [CivArchiveKindOption.User] = "user",
        [CivArchiveKindOption.File] = "file",
    };

    private static readonly IReadOnlyDictionary<CivArchivePeriodOption, string> PeriodMap = new Dictionary<
        CivArchivePeriodOption,
        string
    >
    {
        [CivArchivePeriodOption.Week] = "week",
        [CivArchivePeriodOption.Month] = "month",
        [CivArchivePeriodOption.Quarter] = "quarter",
        [CivArchivePeriodOption.Half] = "half",
        [CivArchivePeriodOption.Year] = "year",
        [CivArchivePeriodOption.All] = "all",
    };

    private static readonly IReadOnlyDictionary<CivArchiveRatingOption, string> RatingMap = new Dictionary<
        CivArchiveRatingOption,
        string
    >
    {
        [CivArchiveRatingOption.All] = "all",
        [CivArchiveRatingOption.Explicit] = "explicit",
        [CivArchiveRatingOption.Safe] = "safe",
    };

    private static readonly IReadOnlyDictionary<CivArchivePlatformStatusOption, string> PlatformStatusMap =
        new Dictionary<CivArchivePlatformStatusOption, string>
        {
            [CivArchivePlatformStatusOption.All] = "all",
            [CivArchivePlatformStatusOption.Available] = "available",
            [CivArchivePlatformStatusOption.Deleted] = "deleted",
        };

    private static readonly IReadOnlyDictionary<CivArchivePlatformOption, string> PlatformMap =
        new Dictionary<CivArchivePlatformOption, string>
        {
            [CivArchivePlatformOption.All] = "all",
            [CivArchivePlatformOption.Civitai] = "civitai",
            [CivArchivePlatformOption.Tensorart] = "tensorart",
            [CivArchivePlatformOption.Tensorhub] = "tensorhub",
            [CivArchivePlatformOption.Seaart] = "seaart",
            [CivArchivePlatformOption.Civision] = "civision",
            [CivArchivePlatformOption.Pixai] = "pixai",
            [CivArchivePlatformOption.Tungsten] = "tungsten",
            [CivArchivePlatformOption.Yodayo] = "yodayo",
            [CivArchivePlatformOption.Moescape] = "moescape",
            [CivArchivePlatformOption.Shakker] = "shakker",
            [CivArchivePlatformOption.Huggingface] = "huggingface",
            [CivArchivePlatformOption.Modelscope] = "modelscope",
            [CivArchivePlatformOption.ModelscopeCn] = "modelscope_cn",
        };

    public static string ToApiString(this CivArchiveSortOption value) => SortMap[value];

    public static string ToApiString(this CivArchiveKindOption value) => KindMap[value];

    public static string ToApiString(this CivArchivePeriodOption value) => PeriodMap[value];

    public static string ToApiString(this CivArchiveRatingOption value) => RatingMap[value];

    public static string ToApiString(this CivArchivePlatformStatusOption value) => PlatformStatusMap[value];

    public static string ToApiString(this CivArchivePlatformOption value) => PlatformMap[value];

    public static CivArchiveSortOption ParseSort(string value) =>
        ParseFromMap(value, SortMap, CivArchiveSortOption.Top);

    public static CivArchiveKindOption ParseKind(string value) =>
        ParseFromMap(value, KindMap, CivArchiveKindOption.All);

    public static CivArchivePeriodOption ParsePeriod(string value) =>
        ParseFromMap(value, PeriodMap, CivArchivePeriodOption.All);

    public static CivArchiveRatingOption ParseRating(string value) =>
        ParseFromMap(value, RatingMap, CivArchiveRatingOption.Safe);

    public static CivArchivePlatformStatusOption ParsePlatformStatus(string value) =>
        ParseFromMap(value, PlatformStatusMap, CivArchivePlatformStatusOption.All);

    public static CivArchivePlatformOption ParsePlatform(string value) =>
        ParseFromMap(value, PlatformMap, CivArchivePlatformOption.All);

    private static TEnum ParseFromMap<TEnum>(
        string? value,
        IReadOnlyDictionary<TEnum, string> map,
        TEnum fallback
    )
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        foreach (var kvp in map)
        {
            if (string.Equals(kvp.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Key;
            }
        }

        return fallback;
    }
}
