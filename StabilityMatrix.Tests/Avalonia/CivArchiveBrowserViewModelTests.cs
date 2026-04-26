using FluentAvalonia.UI.Media.Animation;
using NSubstitute;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.CivArchive;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class CivArchiveBrowserViewModelTests
{
    [TestMethod]
    public void Constructor_LoadsExpectedDefaults()
    {
        var vm = CreateViewModel(Substitute.For<ICivArchiveApiClient>(), out _, out _);

        vm.OnLoaded();

        Assert.AreEqual(CivArchivePlatformOption.All, vm.SelectedPlatform?.Value);
        Assert.AreEqual(CivArchiveSortOption.Top, vm.SelectedSort?.Value);
        Assert.AreEqual(CivArchivePeriodOption.All, vm.SelectedPeriod?.Value);
        Assert.AreEqual(CivArchiveRatingOption.Safe, vm.SelectedRating?.Value);
        Assert.AreEqual(CivArchiveKindOption.All, vm.SelectedKind?.Value);
    }

    [TestMethod]
    public async Task ChangingFilters_TriggersNewQueryAndResetsPaging()
    {
        var apiClient = Substitute.For<ICivArchiveApiClient>();
        var recordedFilters = new List<CivArchiveSearchFilters>();
        apiClient
            .SearchAsync(Arg.Any<CivArchiveSearchFilters>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var filters = call.Arg<CivArchiveSearchFilters>();
                recordedFilters.Add(filters);
                return Task.FromResult(CreateSearchResponse(filters.Page));
            });

        var vm = CreateViewModel(apiClient, out _, out _);
        vm.OnLoaded();

        await vm.SearchModelsCommand.ExecuteAsync(false);
        await vm.LoadNextPageAsync();

        vm.SelectedSort = vm.AllSorts.First(x => x.Value == CivArchiveSortOption.Newest);
        await Task.Delay(50);

        // 3 calls: initial fetch, LoadNextPage, sort change.
        // (No saved selections means ApplyFilterOptions doesn't trigger a redundant re-fetch.)
        Assert.AreEqual(3, recordedFilters.Count);
        Assert.AreEqual(1, recordedFilters[0].Page);
        Assert.AreEqual(2, recordedFilters[1].Page);
        Assert.AreEqual(1, recordedFilters[2].Page);
        Assert.AreEqual(CivArchiveSortOption.Newest, recordedFilters[2].Sort);
    }

    [TestMethod]
    public async Task OpenResult_UserPivotSetsUsernameFilter()
    {
        var apiClient = Substitute.For<ICivArchiveApiClient>();
        CivArchiveSearchFilters? recordedFilter = null;
        apiClient
            .SearchAsync(Arg.Any<CivArchiveSearchFilters>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                recordedFilter = call.Arg<CivArchiveSearchFilters>();
                return Task.FromResult(CreateSearchResponse(1));
            });

        var vm = CreateViewModel(apiClient, out _, out _);
        vm.OnLoaded();

        await vm.OpenResultCommand.ExecuteAsync(
            new CivArchiveSearchResult
            {
                Id = "user-1",
                Name = "artist-name",
                KindRaw = "user",
                Username = "artist-name",
                Url = "/users/artist-name",
            }
        );

        Assert.IsNotNull(recordedFilter);
        Assert.AreEqual("artist-name", recordedFilter.Username);
    }

    [TestMethod]
    public async Task OpenResult_VersionNavigatesToDetailsPage()
    {
        var apiClient = Substitute.For<ICivArchiveApiClient>();
        var navigationService = Substitute.For<INavigationService<MainWindowViewModel>>();
        var vm = CreateViewModel(apiClient, out _, out var serviceManager, navigationService);

        await vm.OpenResultCommand.ExecuteAsync(
            new CivArchiveSearchResult
            {
                Id = "version-1",
                Name = "Version",
                KindRaw = "version",
                Url = "/models/443821?modelVersionId=2581228",
            }
        );

        navigationService
            .Received(1)
            .NavigateTo(
                Arg.Is<ViewModelBase>(x =>
                    x.GetType() == typeof(CivArchiveDetailsPageViewModel)
                    && ((CivArchiveDetailsPageViewModel)x).RelativeUrl.Contains("modelVersionId=2581228")
                ),
                Arg.Any<NavigationTransitionInfo>()
            );
    }

    [TestMethod]
    public async Task ChangingFilterWhileLoading_QueuesRefreshWithLatestFilter()
    {
        var apiClient = Substitute.For<ICivArchiveApiClient>();
        var delayedResponse = new TaskCompletionSource<CivArchiveSearchResponse>();
        var recordedFilters = new List<CivArchiveSearchFilters>();

        apiClient
            .SearchAsync(Arg.Any<CivArchiveSearchFilters>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var filters = call.Arg<CivArchiveSearchFilters>();
                recordedFilters.Add(filters);

                // Delay call #2 — that's the explicit ExecuteAsync at line below (line 149
                // equivalent), so the sort change happens while it's in flight and gets
                // queued. With the redundant init re-fetch removed, the second call is
                // now the queueable one (used to be call #3).
                return recordedFilters.Count switch
                {
                    2 => delayedResponse.Task,
                    _ => Task.FromResult(CreateSearchResponse(filters.Page)),
                };
            });

        var vm = CreateViewModel(apiClient, out _, out _);
        vm.OnLoaded();

        await vm.SearchModelsCommand.ExecuteAsync(false);

        var loadingSearch = vm.SearchModelsCommand.ExecuteAsync(false);
        vm.SelectedSort = vm.AllSorts.First(x => x.Value == CivArchiveSortOption.Newest);

        delayedResponse.SetResult(CreateSearchResponse(1));
        await loadingSearch;

        // 3 calls: initial, in-flight (delayed), queued sort-change refresh.
        Assert.AreEqual(3, recordedFilters.Count);
        Assert.AreEqual(CivArchiveSortOption.Top, recordedFilters[1].Sort);
        Assert.AreEqual(CivArchiveSortOption.Newest, recordedFilters[2].Sort);
    }

    [TestMethod]
    public async Task DownloadModel_UsesPrimaryFileUrlNameAndHash()
    {
        var apiClient = Substitute.For<ICivArchiveApiClient>();
        var modelImportService = Substitute.For<IModelImportService>();
        var settingsManager = Substitute.For<ISettingsManager>();
        var model = CreateDetailsModel(
            new CivArchiveModelFile
            {
                Name = "realDream_sdxl7.ckpt",
                DownloadUrl = "https://example.org/download/realDream_sdxl7.ckpt",
                Sha256 = "63b1db60611f52c4fbb2cade67dbdf4029c6620c5b22f2a4ddb27a47d7601953",
                IsPrimary = true,
            }
        );

        IReadOnlyList<Uri>? capturedUris = null;
        string? capturedFileName = null;
        Action<TrackedDownload>? configureDownload = null;

        apiClient
            .GetModelDetailsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new CivArchiveModelDetailsResponse { Model = model });
        apiClient.GetAbsoluteUri(Arg.Any<string>()).Returns(call => new Uri(call.Arg<string>()));
        settingsManager.IsLibraryDirSet.Returns(true);
        settingsManager.ModelsDirectory.Returns(Path.GetTempPath());
        modelImportService
            .DoCustomImport(
                Arg.Do<IEnumerable<Uri>>(uris => capturedUris = uris.ToList()),
                Arg.Do<string>(fileName => capturedFileName = fileName),
                Arg.Any<DirectoryPath>(),
                Arg.Any<Uri?>(),
                Arg.Any<string?>(),
                Arg.Any<ConnectedModelInfo?>(),
                Arg.Do<Action<TrackedDownload>?>(action => configureDownload = action)
            )
            .Returns(Task.CompletedTask);

        var vm = CreateDetailsViewModel(apiClient, modelImportService, settingsManager);
        vm.RelativeUrl = "/models/153568?modelVersionId=2053273";

        await vm.OnLoadedAsync();
        await vm.DownloadModelCommand.ExecuteAsync(null);

        Assert.IsTrue(vm.HasDownloadUrl);
        Assert.AreEqual("realDream_sdxl7.ckpt", capturedFileName);
        Assert.AreEqual("https://example.org/download/realDream_sdxl7.ckpt", capturedUris?[0].ToString());

        var download = new TrackedDownload
        {
            Id = Guid.NewGuid(),
            SourceUrl = capturedUris![0],
            DownloadDirectory = new DirectoryPath(Path.GetTempPath()),
            FileName = capturedFileName!,
            TempFileName = $"{capturedFileName}.tmp",
        };
        configureDownload?.Invoke(download);
        Assert.AreEqual(model.Version?.Files[0].Sha256, download.ExpectedHashSha256);
    }

    [TestMethod]
    public async Task DownloadModel_UsesFileMirrorUrlWhenDirectUrlIsMissing()
    {
        var apiClient = Substitute.For<ICivArchiveApiClient>();
        var modelImportService = Substitute.For<IModelImportService>();
        var settingsManager = Substitute.For<ISettingsManager>();
        var model = CreateDetailsModel(
            new CivArchiveModelFile
            {
                Name = "mirror-only.safetensors",
                Mirrors =
                [
                    new CivArchiveFileMirror
                    {
                        Source = "civitai",
                        Url = "https://example.org/mirror/mirror-only.safetensors",
                    },
                ],
            }
        );

        IReadOnlyList<Uri>? capturedUris = null;

        apiClient
            .GetModelDetailsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new CivArchiveModelDetailsResponse { Model = model });
        apiClient.GetAbsoluteUri(Arg.Any<string>()).Returns(call => new Uri(call.Arg<string>()));
        settingsManager.IsLibraryDirSet.Returns(true);
        settingsManager.ModelsDirectory.Returns(Path.GetTempPath());
        modelImportService
            .DoCustomImport(
                Arg.Do<IEnumerable<Uri>>(uris => capturedUris = uris.ToList()),
                Arg.Any<string>(),
                Arg.Any<DirectoryPath>(),
                Arg.Any<Uri?>(),
                Arg.Any<string?>(),
                Arg.Any<ConnectedModelInfo?>(),
                Arg.Any<Action<TrackedDownload>?>()
            )
            .Returns(Task.CompletedTask);

        var vm = CreateDetailsViewModel(apiClient, modelImportService, settingsManager);
        vm.RelativeUrl = "/models/153568?modelVersionId=2053273";

        await vm.OnLoadedAsync();
        await vm.DownloadModelCommand.ExecuteAsync(null);

        Assert.IsTrue(vm.HasDownloadUrl);
        Assert.AreEqual("https://example.org/mirror/mirror-only.safetensors", capturedUris?[0].ToString());
    }

    [TestMethod]
    public void ParseSearchQuery_PlainQuery_ReturnsQueryOnly()
    {
        var (query, tags, username) = CivArchiveBrowserViewModel.ParseSearchQuery("dragon style");

        Assert.AreEqual("dragon style", query);
        Assert.AreEqual(string.Empty, tags);
        Assert.AreEqual(string.Empty, username);
    }

    [TestMethod]
    public void ParseSearchQuery_AtToken_ExtractsUsername()
    {
        var (query, tags, username) = CivArchiveBrowserViewModel.ParseSearchQuery("dragon @sinatra");

        Assert.AreEqual("dragon", query);
        Assert.AreEqual(string.Empty, tags);
        Assert.AreEqual("sinatra", username);
    }

    [TestMethod]
    public void ParseSearchQuery_HashTokens_ExtractedAsCommaJoined()
    {
        var (query, tags, username) = CivArchiveBrowserViewModel.ParseSearchQuery("painting #anime #sdxl");

        Assert.AreEqual("painting", query);
        Assert.AreEqual("anime,sdxl", tags);
        Assert.AreEqual(string.Empty, username);
    }

    [TestMethod]
    public void ParseSearchQuery_MultipleAtTokens_LastWins()
    {
        var (_, _, username) = CivArchiveBrowserViewModel.ParseSearchQuery("@alice @bob");

        Assert.AreEqual("bob", username);
    }

    [TestMethod]
    public void ParseSearchQuery_MixedTokens_AllExtracted()
    {
        var (query, tags, username) = CivArchiveBrowserViewModel.ParseSearchQuery(
            "dragon #anime @sinatra #sdxl knight"
        );

        Assert.AreEqual("dragon knight", query);
        Assert.AreEqual("anime,sdxl", tags);
        Assert.AreEqual("sinatra", username);
    }

    [TestMethod]
    public void ParseSearchQuery_EmptyOrWhitespace_ReturnsEmptyTuple()
    {
        var empty = CivArchiveBrowserViewModel.ParseSearchQuery("");
        var whitespace = CivArchiveBrowserViewModel.ParseSearchQuery("   ");

        Assert.AreEqual(string.Empty, empty.query);
        Assert.AreEqual(string.Empty, empty.tags);
        Assert.AreEqual(string.Empty, empty.username);
        Assert.AreEqual(string.Empty, whitespace.query);
    }

    [TestMethod]
    public void ParseSearchQuery_BareSigil_KeptAsRegularToken()
    {
        // A lone "@" or "#" with nothing after isn't a username/tag prefix —
        // the parser leaves it as part of the regular query.
        var (query, tags, username) = CivArchiveBrowserViewModel.ParseSearchQuery("foo @ # bar");

        Assert.AreEqual("foo @ # bar", query);
        Assert.AreEqual(string.Empty, tags);
        Assert.AreEqual(string.Empty, username);
    }

    private static CivArchiveBrowserViewModel CreateViewModel(
        ICivArchiveApiClient apiClient,
        out Settings settings,
        out IServiceManager<ViewModelBase> serviceManager,
        INavigationService<MainWindowViewModel>? navigationService = null
    )
    {
        var localSettings = new Settings();
        settings = localSettings;
        var settingsManager = Substitute.For<ISettingsManager>();
        settingsManager.Settings.Returns(localSettings);
        settingsManager.IsLibraryDirSet.Returns(true);
        settingsManager
            .When(x => x.Transaction(Arg.Any<Action<Settings>>(), Arg.Any<bool>()))
            .Do(call => call.Arg<Action<Settings>>()(localSettings));

        serviceManager = new TestServiceManager<ViewModelBase>(
            new CivArchiveDetailsPageViewModel(
                Substitute.For<ICivArchiveApiClient>(),
                navigationService ?? Substitute.For<INavigationService<MainWindowViewModel>>(),
                Substitute.For<IServiceManager<ViewModelBase>>(),
                Substitute.For<IModelImportService>(),
                Substitute.For<ISettingsManager>(),
                Substitute.For<INotificationService>(),
                CreateModelIndexServiceStub()
            )
        );

        return new CivArchiveBrowserViewModel(
            apiClient,
            settingsManager,
            serviceManager,
            navigationService ?? Substitute.For<INavigationService<MainWindowViewModel>>(),
            CreateModelIndexServiceStub()
        );
    }

    private static IModelIndexService CreateModelIndexServiceStub()
    {
        var stub = Substitute.For<IModelIndexService>();
        stub.ModelIndexSha256Hashes.Returns(new HashSet<string>());
        stub.ModelIndexBlake3Hashes.Returns(new HashSet<string>());
        return stub;
    }

    private static CivArchiveDetailsPageViewModel CreateDetailsViewModel(
        ICivArchiveApiClient apiClient,
        IModelImportService modelImportService,
        ISettingsManager settingsManager
    )
    {
        return new CivArchiveDetailsPageViewModel(
            apiClient,
            Substitute.For<INavigationService<MainWindowViewModel>>(),
            Substitute.For<IServiceManager<ViewModelBase>>(),
            modelImportService,
            settingsManager,
            Substitute.For<INotificationService>(),
            CreateModelIndexServiceStub()
        );
    }

    private static CivArchiveModelDetails CreateDetailsModel(CivArchiveModelFile file)
    {
        return new CivArchiveModelDetails
        {
            Name = "Real Dream",
            Type = "Checkpoint",
            Version = new CivArchiveModelVersion
            {
                Name = "SDXL 7",
                BaseModel = "SDXL 1.0",
                Files = [file],
                Images = [new CivArchiveModelImage { Url = "https://example.org/preview.webp" }],
            },
        };
    }

    private static CivArchiveSearchResponse CreateSearchResponse(int page)
    {
        return new CivArchiveSearchResponse
        {
            Results =
            [
                new CivArchiveSearchResult
                {
                    Id = $"item-{page}-1",
                    Name = $"Item {page}",
                    KindRaw = "version",
                    Url = $"/models/{page}?modelVersionId={page}",
                },
            ],
            FilterOptions = new CivArchiveFilterOptions
            {
                BaseModels = ["Illustrious", "Pony"],
                ModelTypes = ["LORA", "Checkpoint"],
            },
            EffectiveFilters = new CivArchiveSearchFilters { Page = page },
            CanonicalUrl = "https://civarchive.com/top-models",
            Hits = 1,
            TotalHits = 4,
        };
    }

    private sealed class TestServiceManager<T>(T instance) : IServiceManager<T>
    {
        public IServiceManager<T> Register<TService>(TService serviceInstance)
            where TService : T => this;

        public IServiceManager<T> Register<TService>(Func<TService> provider)
            where TService : T => this;

        public void Register(Type type, Func<T> providerFunc) { }

        public IServiceManager<T> RegisterProvider<TService>(IServiceProvider provider)
            where TService : notnull, T => this;

        public IServiceManager<T> RegisterScoped<TService>(Func<IServiceProvider, TService> provider)
            where TService : T => this;

        public IServiceManagerScope<T> CreateScope() => throw new NotImplementedException();

        public T Get(Type serviceType) => instance;

        public TService Get<TService>()
            where TService : T => (TService)instance!;

        public IServiceManager<T> RegisterScoped(Type type, Func<IServiceProvider, T> provider) => this;
    }
}
