using System.Diagnostics;
using System.Text.RegularExpressions;
using AsyncAwaitBestPractices;
using AutoComplete.Builders;
using AutoComplete.Clients.IndexSearchers;
using AutoComplete.DataStructure;
using AutoComplete.Domain;
using Avalonia.Controls.Notifications;
using Injectio.Attributes;
using Nito.AsyncEx;
using NLog;
using StabilityMatrix.Avalonia.Controls.CodeCompletion;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Models.Tokens;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Models.TagCompletion;

[RegisterSingleton<ICompletionProvider, CompletionProvider>]
public partial class CompletionProvider : ICompletionProvider
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    [GeneratedRegex(@"([\[\]()<>])")]
    private static partial Regex BracketsRegex();

    private readonly ISettingsManager settingsManager;
    private readonly INotificationService notificationService;
    private readonly IModelIndexService modelIndexService;
    private readonly IDownloadService downloadService;

    private readonly AsyncLock loadLock = new();
    private readonly Dictionary<string, TagCsvEntry> entries = new();

    private InMemoryIndexSearcher? searcher;

    /// <inheritdoc />
    public CompletionType AvailableCompletionTypes
    {
        get
        {
            var types = CompletionType.ExtraNetwork | CompletionType.ExtraNetworkType;
            if (searcher is not null)
            {
                types |= CompletionType.Tag;
            }
            return types;
        }
    }

    public Func<ICompletionData, string>? PrepareInsertionText { get; }

    public CompletionProvider(
        ISettingsManager settingsManager,
        INotificationService notificationService,
        IModelIndexService modelIndexService,
        IDownloadService downloadService
    )
    {
        this.settingsManager = settingsManager;
        this.notificationService = notificationService;
        this.modelIndexService = modelIndexService;
        this.downloadService = downloadService;

        PrepareInsertionText = PrepareInsertionText_Process;

        // Attach to load from set file on initial settings load
        settingsManager.Loaded += (_, _) => UpdateTagCompletionCsv();

        // Also load when TagCompletionCsv property changes
        settingsManager.SettingsPropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(Settings.TagCompletionCsv))
            {
                UpdateTagCompletionCsv();
            }
        };

        // If library already loaded, start a background load
        if (settingsManager.IsLibraryDirSet)
        {
            UpdateTagCompletionCsv();
        }

        return;

        void UpdateTagCompletionCsv()
        {
            var csvPath = settingsManager.Settings.TagCompletionCsv;
            if (csvPath is null)
                return;

            var fullPath = settingsManager.TagsDirectory.JoinFile(csvPath);
            BackgroundLoadFromFile(fullPath);
        }
    }

    private string PrepareInsertionText_Process(ICompletionData data)
    {
        var text = data.Text;

        // For tags and if enabled, replace underscores with spaces
        if (data is TagCompletionData && settingsManager.Settings.IsCompletionRemoveUnderscoresEnabled)
        {
            // Remove underscores
            text = text.Replace("_", " ");
        }

        // (Only for non model types)
        // For bracket type character, escape it
        if (data is not ModelCompletionData)
        {
            text = BracketsRegex().Replace(text, @"\$1");
        }

        return text;
    }

    /// <inheritdoc />
    public void BackgroundLoadFromFile(FilePath path, bool recreate = false)
    {
        LoadFromFile(path, recreate)
            .SafeFireAndForget(
                onException: exception =>
                {
                    const string title = "Failed to load tag completion file";
                    if (Debugger.IsAttached)
                    {
                        Debug.Fail(title);
                    }
                    Logger.Warn(exception, title);
                    notificationService.Show(
                        title + $" {path.Name}",
                        exception.Message,
                        NotificationType.Error
                    );
                },
                true
            );
    }

    /// <inheritdoc />
    public async Task Setup()
    {
        var tagsDir = settingsManager.TagsDirectory;
        tagsDir.Create();

        // If tagsDir is empty and no selected, download defaults
        if (
            !tagsDir.Info.EnumerateFiles().Any()
            && (
                settingsManager.Settings.TagCompletionCsv is null
                || !tagsDir.JoinFile(settingsManager.Settings.TagCompletionCsv).Exists
            )
        )
        {
            foreach (var remoteCsv in Assets.DefaultCompletionTags)
            {
                var fileName = remoteCsv.Url.Segments.Last();
                Logger.Info(
                    "Downloading default tag source {Name} [{Hash}]",
                    fileName,
                    remoteCsv.HashSha256[..7]
                );
                await downloadService.DownloadToFileAsync(
                    remoteCsv.Url.ToString(),
                    tagsDir.JoinFile(fileName)
                );
            }

            var defaultFile = tagsDir.JoinFile("danbooru_e621_merged.csv");
            if (!defaultFile.Exists)
            {
                Logger.Warn("Failed to download default tag source");
                return;
            }

            // Set default file as selected
            settingsManager.Settings.TagCompletionCsv = defaultFile.Name;
            Logger.Debug("Tag completion source set to {Name}", defaultFile.Name);

            // Load default file
            BackgroundLoadFromFile(defaultFile);
        }
        else
        {
            var newDefaultFile = tagsDir.JoinFile("danbooru_e621_merged.csv");
            if (newDefaultFile.Exists)
            {
                return;
            }

            var newRemoteCsv = Assets.DefaultCompletionTags[^1];
            var fileName = newRemoteCsv.Url.Segments.Last();
            Logger.Info(
                "Downloading new default tag source {Name} [{Hash}]",
                fileName,
                newRemoteCsv.HashSha256[..7]
            );
            await downloadService.DownloadToFileAsync(
                newRemoteCsv.Url.ToString(),
                tagsDir.JoinFile(fileName)
            );

            notificationService.Show(
                "New autocomplete tag source downloaded",
                "You can activate this in Settings -> Inference -> Auto Completion",
                expiration: TimeSpan.FromSeconds(8)
            );
        }
    }

    /// <inheritdoc />
    public async Task LoadFromFile(FilePath path, bool recreate = false)
    {
        using var _ = await loadLock.LockAsync();

        // Get Blake3 hash of file
        var hash = await FileHash.GetBlake3Async(path);

        Logger.Trace("Loading tags from {Path} with Blake3 hash {Hash}", path, hash);

        // Check for AppData/StabilityMatrix/Temp/Tags/<hash>/*.bin
        var tempTagsDir = GlobalConfig.HomeDir.JoinDir("Temp", "Tags");
        var hashDir = tempTagsDir.JoinDir(hash);
        hashDir.Create();

        var headerFile = hashDir.JoinFile("header.bin");
        var indexFile = hashDir.JoinFile("index.bin");

        entries.Clear();

        var timer = Stopwatch.StartNew();

        // If directory or any file is missing, rebuild the index
        if (recreate || !(hashDir.Exists && headerFile.Exists && indexFile.Exists))
        {
            Logger.Debug("Creating new index for {Path}", hashDir);

            await using var headerStream = headerFile.Info.OpenWrite();
            await using var indexStream = indexFile.Info.OpenWrite();

            var builder = new IndexBuilder(headerStream, indexStream);

            // Parse csv
            await using var csvStream = path.Info.OpenRead();
            var parser = new TagCsvParser(csvStream);

            await foreach (var entry in parser.ParseAsync())
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                    continue;

                // Add to index
                builder.Add(entry.Name);
                // Add to local dictionary
                entries.Add(entry.Name, entry);
            }

            await Task.Run(builder.Build);
        }
        else
        {
            // Otherwise just load the dictionary
            Logger.Debug("Loading existing index for {Path}", hashDir);

            await using var csvStream = path.Info.OpenRead();
            var parser = new TagCsvParser(csvStream);

            await foreach (var entry in parser.ParseAsync())
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                    continue;

                // Add to local dictionary
                entries.Add(entry.Name, entry);
            }
        }

        searcher = new InMemoryIndexSearcher(headerFile, indexFile);
        searcher.Init();

        var elapsed = timer.Elapsed;

        Logger.Info(
            "Loaded {Count} tags for {Path} in {Time:F2}s",
            entries.Count,
            path.Name,
            elapsed.TotalSeconds
        );
    }

    /// <inheritdoc />
    public IEnumerable<ICompletionData> GetCompletions(
        TextCompletionRequest completionRequest,
        int itemsCount,
        bool suggest
    )
    {
        if (completionRequest.Type == CompletionType.Tag)
        {
            return GetCompletionTags(completionRequest.Text, itemsCount, suggest);
        }

        if (completionRequest.Type == CompletionType.ExtraNetwork)
        {
            return GetCompletionNetworks(
                completionRequest.ExtraNetworkTypes,
                completionRequest.Text,
                itemsCount
            );
        }

        if (completionRequest.Type == CompletionType.ExtraNetworkType)
        {
            return GetCompletionNetworkTypes(completionRequest.Text);
        }

        throw new InvalidOperationException();
    }

    private IEnumerable<ICompletionData> GetCompletionNetworks(
        PromptExtraNetworkType networkType,
        string searchTerm,
        int itemsCount
    )
    {
        var folderTypes = Enum.GetValues(typeof(PromptExtraNetworkType))
            .Cast<PromptExtraNetworkType>()
            .Where(f => networkType.HasFlag(f))
            .Select(network => network.ConvertTo<SharedFolderType>())
            // Convert back to bit flags
            .Aggregate((a, b) => a | b);

        var models = modelIndexService.FindByModelType(folderTypes);

        var matches = models
            .Where(model => model.FileName.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
            .Select(model => ModelCompletionData.FromLocalModel(model, networkType))
            .Take(itemsCount);

        return matches;
    }

    private IEnumerable<ICompletionData> GetCompletionNetworkTypes(string searchTerm)
    {
        var availableTypes = new[]
        {
            (PromptExtraNetworkType.Lora, "lora"),
            (PromptExtraNetworkType.LyCORIS, "lyco"),
            (PromptExtraNetworkType.Embedding, "embedding"),
        };

        return availableTypes
            .Where(type =>
                type.Item1.GetStringValue().StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase)
            )
            .Select(type => new ModelTypeCompletionData(type.Item2, type.Item1));
    }

    private IEnumerable<ICompletionData> GetCompletionTags(string searchTerm, int itemsCount, bool suggest)
    {
        if (searcher is null)
        {
            throw new InvalidOperationException("Index is not loaded");
        }

        var timer = Stopwatch.StartNew();

        var searchOptions = new SearchOptions
        {
            Term = searchTerm,
            MaxItemCount = itemsCount,
            SuggestWhenFoundStartsWith = suggest,
        };

        var result = searcher.Search(searchOptions);

        // No results
        if (result.ResultType == TrieNodeSearchResultType.NotFound)
        {
            Logger.Trace("No results for {Term}", searchTerm);
            return Array.Empty<ICompletionData>();
        }

        // Is null for some reason?
        if (result.Items is null)
        {
            Logger.Warn("Got null results for {Term}", searchTerm);
            return Array.Empty<ICompletionData>();
        }

        Logger.Trace("Got {Count} results for {Term}", result.Items.Length, searchTerm);

        // Get entry for each result
        var completions = new List<ICompletionData>();
        foreach (var item in result.Items)
        {
            if (entries.TryGetValue(item, out var entry))
            {
                var entryType = TagTypeExtensions.FromE621(entry.Type.GetValueOrDefault(-1));
                completions.Add(new TagCompletionData(entry.Name!, entryType));
            }
        }

        timer.Stop();
        Logger.Trace("Completions for {Term} took {Time:F2}ms", searchTerm, timer.Elapsed.TotalMilliseconds);

        return completions;
    }
}
