using System.Text.Json;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Models.Api.Lykos.Analytics;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Helper.Analytics;

/// <summary>
/// Keeps <see cref="AnalyticsUsageCounters"/> in memory and mirrors them to
/// <c>analytics-usage.json</c> in the library directory, debounced so frequent events do not
/// hammer the disk. Counters survive restarts until their report is sent.
/// </summary>
[RegisterSingleton<IAnalyticsUsageTracker, AnalyticsUsageTracker>]
public class AnalyticsUsageTracker : IAnalyticsUsageTracker, IDisposable
{
    private const string FileName = "analytics-usage.json";
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<AnalyticsUsageTracker> logger;
    private readonly ISettingsManager settingsManager;
    private readonly object sync = new();
    private readonly Timer saveTimer;

    private AnalyticsUsageCounters counters = new();
    private bool isLoaded;
    private bool isDirty;

    public AnalyticsUsageTracker(ILogger<AnalyticsUsageTracker> logger, ISettingsManager settingsManager)
    {
        this.logger = logger;
        this.settingsManager = settingsManager;

        saveTimer = new Timer(_ => Save(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        settingsManager.RegisterOnLibraryDirSet(_ => Load());
    }

    private AnalyticsSettings Settings => settingsManager.Settings.Analytics;

    private string FilePath => Path.Combine(settingsManager.LibraryDir, FileName);

    public void RecordPackageLaunch(string packageName) =>
        Record(AnalyticsCategory.PackageUsage, now => counters.PackageUsage.RecordLaunch(packageName, now));

    public void RecordPackageRun(string packageName, TimeSpan duration) =>
        Record(
            AnalyticsCategory.PackageUsage,
            now => counters.PackageUsage.RecordRun(packageName, duration, now)
        );

    public void RecordInferenceGeneration(string projectType, string? modelFamily) =>
        Record(
            AnalyticsCategory.InferenceUsage,
            now => counters.InferenceUsage.RecordGeneration(projectType, modelFamily, now)
        );

    public void RecordPageVisit(string pageName) =>
        Record(AnalyticsCategory.FeatureUsage, now => counters.FeatureUsage.RecordPageVisit(pageName, now));

    public IReadOnlyList<(AnalyticsCategory Category, UsageAnalyticsRequest Request)> BuildReports(
        DateTimeOffset periodEnd
    )
    {
        var reports = new List<(AnalyticsCategory, UsageAnalyticsRequest)>();

        lock (sync)
        {
            EnsureLoaded();

            if (
                Settings.IsCategoryEnabled(AnalyticsCategory.PackageUsage)
                && counters.PackageUsage is { IsEmpty: false } packages
            )
            {
                reports.Add(
                    (
                        AnalyticsCategory.PackageUsage,
                        new PackageUsageAnalyticsRequest
                        {
                            PeriodStart = packages.PeriodStart ?? periodEnd,
                            PeriodEnd = periodEnd,
                            Launches = new Dictionary<string, int>(packages.Launches),
                            RunMinutes = packages.GetRunMinutes(),
                        }
                    )
                );
            }

            if (
                Settings.IsCategoryEnabled(AnalyticsCategory.InferenceUsage)
                && counters.InferenceUsage is { IsEmpty: false } inference
            )
            {
                reports.Add(
                    (
                        AnalyticsCategory.InferenceUsage,
                        new InferenceUsageAnalyticsRequest
                        {
                            PeriodStart = inference.PeriodStart ?? periodEnd,
                            PeriodEnd = periodEnd,
                            Generations = new Dictionary<string, int>(inference.Generations),
                            ModelFamilies = new Dictionary<string, int>(inference.ModelFamilies),
                        }
                    )
                );
            }

            if (
                Settings.IsCategoryEnabled(AnalyticsCategory.FeatureUsage)
                && counters.FeatureUsage is { IsEmpty: false } features
            )
            {
                reports.Add(
                    (
                        AnalyticsCategory.FeatureUsage,
                        new FeatureUsageAnalyticsRequest
                        {
                            PeriodStart = features.PeriodStart ?? periodEnd,
                            PeriodEnd = periodEnd,
                            PageVisits = new Dictionary<string, int>(features.PageVisits),
                        }
                    )
                );
            }
        }

        return reports;
    }

    public void ResetCategory(AnalyticsCategory category)
    {
        lock (sync)
        {
            EnsureLoaded();

            switch (category)
            {
                case AnalyticsCategory.PackageUsage:
                    counters.PackageUsage = new PackageUsageCounters();
                    break;
                case AnalyticsCategory.InferenceUsage:
                    counters.InferenceUsage = new InferenceUsageCounters();
                    break;
                case AnalyticsCategory.FeatureUsage:
                    counters.FeatureUsage = new FeatureUsageCounters();
                    break;
                default:
                    return;
            }

            MarkDirty();
        }
    }

    private void Record(AnalyticsCategory category, Action<DateTimeOffset> mutate)
    {
        if (!Settings.IsCategoryEnabled(category))
        {
            return;
        }

        lock (sync)
        {
            EnsureLoaded();
            mutate(DateTimeOffset.UtcNow);
            MarkDirty();
        }
    }

    /// <summary>Caller must hold <see cref="sync"/>.</summary>
    private void MarkDirty()
    {
        isDirty = true;
        saveTimer.Change(SaveDelay, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Caller must hold <see cref="sync"/>.</summary>
    private void EnsureLoaded()
    {
        if (!isLoaded && settingsManager.IsLibraryDirSet)
        {
            Load();
        }
    }

    private void Load()
    {
        lock (sync)
        {
            isLoaded = true;

            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    counters = JsonSerializer.Deserialize<AnalyticsUsageCounters>(json, JsonOptions) ?? new();
                }
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Failed to load analytics usage counters, starting fresh");
                counters = new AnalyticsUsageCounters();
            }
        }
    }

    private void Save()
    {
        lock (sync)
        {
            if (!isDirty || !settingsManager.IsLibraryDirSet)
            {
                return;
            }

            try
            {
                File.WriteAllText(FilePath, JsonSerializer.Serialize(counters, JsonOptions));
                isDirty = false;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Failed to save analytics usage counters");
            }
        }
    }

    public void Dispose()
    {
        saveTimer.Dispose();
        Save();
        GC.SuppressFinalize(this);
    }
}
