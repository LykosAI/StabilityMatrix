using FluentAvalonia.UI.Media.Animation;
using NSubstitute;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models.Api.CivArchive;
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

        Assert.AreEqual(4, recordedFilters.Count);
        Assert.AreEqual(1, recordedFilters[0].Page);
        Assert.AreEqual(1, recordedFilters[1].Page);
        Assert.AreEqual(2, recordedFilters[2].Page);
        Assert.AreEqual(1, recordedFilters[3].Page);
        Assert.AreEqual(CivArchiveSortOption.Newest, recordedFilters[3].Sort);
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
                Substitute.For<INotificationService>()
            )
        );

        return new CivArchiveBrowserViewModel(
            apiClient,
            settingsManager,
            serviceManager,
            navigationService ?? Substitute.For<INavigationService<MainWindowViewModel>>()
        );
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
