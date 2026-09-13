using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.CivitTRPC;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class CivitBrowserFilterTests
{
    private SynchronizationContext? originalContext;
    private readonly ISettingsManager settings = Substitute.For<ISettingsManager>();
    private readonly ICivitApi api = Substitute.For<ICivitApi>();
    private readonly IModelIndexService modelIndex = Substitute.For<IModelIndexService>();
    private readonly IServiceManager<ViewModelBase> factory = Substitute.For<
        IServiceManager<ViewModelBase>
    >();

    [TestInitialize]
    public void Initialize()
    {
        originalContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new ImmediateSynchronizationContext());
        settings.Settings.Returns(new Settings());
    }

    [TestCleanup]
    public void Cleanup() => SynchronizationContext.SetSynchronizationContext(originalContext);

    [TestMethod]
    public async Task NextAndPreviousModel_KeepBaseModelFilter()
    {
        var first = Model(1, Version(11, "Illustrious"), Version(12, "Pony"));
        var second = Model(2, Version(21, "SD 1.5"), Version(22, "pony"));
        api.GetModelById(1).Returns(first);
        api.GetModelById(2).Returns(second);
        using var vm = Details(first);
        vm.BaseModelFilter = ["Pony"];
        await LoadDetails(vm);

        Assert.AreEqual(12, vm.SelectedVersion?.ModelVersion.Id);
        await vm.NextModel();
        Assert.AreEqual(1, vm.CurrentIndex);
        Assert.AreEqual(22, vm.SelectedVersion?.ModelVersion.Id);
        Assert.AreEqual(1, vm.ModelVersions.Count);
        await vm.PreviousModel();
        Assert.AreEqual(0, vm.CurrentIndex);
        Assert.AreEqual(12, vm.SelectedVersion?.ModelVersion.Id);
        Assert.AreEqual(1, vm.ModelVersions.Count);
    }

    [TestMethod]
    public async Task InitialMetadataRefresh_AlsoKeepsBaseModelFilter()
    {
        api.GetModelById(1).Returns(Model(1, Version(11, "Illustrious"), Version(12, "Pony")));
        using var vm = Details(new CivitModel { Id = 1 });
        vm.BaseModelFilter = ["Pony"];
        await LoadDetails(vm);

        Assert.AreEqual(12, vm.SelectedVersion?.ModelVersion.Id);
        Assert.AreEqual(1, vm.ModelVersions.Count);
    }

    [TestMethod]
    public async Task UnfilteredDetails_KeepAllArchitectures()
    {
        using var vm = Details(Model(1, Version(11, "Illustrious"), Version(12, "Pony")));
        await LoadDetails(vm);

        Assert.AreEqual(2, vm.ModelVersions.Count);
        Assert.AreEqual(11, vm.SelectedVersion?.ModelVersion.Id);
    }

    [TestMethod]
    public async Task BaseModelFilter_ComposesWithEarlyAccessAndInstalledFilters()
    {
        var early = Version(12, "Pony");
        early.EarlyAccessDeadline = DateTimeOffset.UtcNow.AddDays(7);
        using var vm = Details(Model(1, Version(11, "Illustrious"), early, Version(13, "Pony")));
        vm.BaseModelFilter = ["Pony"];
        vm.HideEarlyAccess = true;
        vm.HideInstalledModels = true;
        await LoadDetails(vm);

        Assert.AreEqual(13, vm.SelectedVersion?.ModelVersion.Id);
        Assert.AreEqual(1, vm.ModelVersions.Count);
        using var installed = new ModelVersionViewModel(modelIndex, Version(14, "Pony"))
        {
            IsInstalled = true,
        };
        Assert.IsFalse((bool)Invoke(vm, "ShouldIncludeVersion", installed)!);
    }

    [TestMethod]
    public async Task NextModel_WithoutMatchingVersions_DoesNotSelectAnotherArchitecture()
    {
        api.GetModelById(2).Returns(Model(2, Version(21, "Illustrious")));
        using var vm = Details(Model(1, Version(11, "Pony")));
        vm.BaseModelFilter = ["Pony"];
        await LoadDetails(vm);
        await vm.NextModel();

        Assert.IsNull(vm.SelectedVersion);
        Assert.AreEqual(0, vm.ModelVersions.Count);
        Assert.AreEqual(0, vm.CivitFiles.Count);
    }

    [TestMethod]
    public void HideEarlyAccess_UsesDeadlineAndCaseInsensitiveAvailability()
    {
        using var browser = Browser();
        browser.HideEarlyAccessModels = true;
        var version = Version(11, "Pony");
        version.Availability = "Public";
        version.EarlyAccessDeadline = DateTimeOffset.UtcNow.AddDays(7);
        using var card = Card(Model(1, version));

        Assert.IsFalse((bool)Invoke(browser, "FilterModelCardsPredicate", card)!);
        version.EarlyAccessDeadline = DateTimeOffset.UtcNow.AddDays(-1);
        Assert.IsTrue((bool)Invoke(browser, "FilterModelCardsPredicate", card)!);
        version.Availability = "earlyaccess";
        Assert.IsFalse((bool)Invoke(browser, "FilterModelCardsPredicate", card)!);
        browser.HideEarlyAccessModels = false;
        Assert.IsTrue((bool)Invoke(browser, "FilterModelCardsPredicate", card)!);
    }

    [TestMethod]
    public void HideEarlyAccess_KeepsModelWithPublicVersion()
    {
        using var browser = Browser();
        browser.HideEarlyAccessModels = true;
        var early = Version(11, "Pony");
        early.EarlyAccessDeadline = DateTimeOffset.UtcNow.AddDays(7);
        using var card = Card(Model(1, early, Version(12, "Pony")));

        Assert.IsTrue((bool)Invoke(browser, "FilterModelCardsPredicate", card)!);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task OpenDetails_UsesResultFilterSnapshot(bool filteredSearch)
    {
        using var browser = Browser();
        var model = Model(1, Version(11, "Pony"), Version(12, "Illustrious"));
        using var details = Details(model);
        factory
            .Get(Arg.Any<Func<CivitDetailsPageViewModel, CivitDetailsPageViewModel>>())
            .Returns(call => call.Arg<Func<CivitDetailsPageViewModel, CivitDetailsPageViewModel>>()(details));

        string[]? requestBaseModels = filteredSearch ? ["Pony"] : null;
        Invoke(browser, "UpdateModelCards", new List<CivitModel>(), requestBaseModels, false);
        if (requestBaseModels is not null)
            requestBaseModels[0] = "Illustrious";

        // A direct-ID search has no base-model restriction even when the dropdown has one.
        browser.SelectedBaseModels.Add("Illustrious");
        await browser.ShowVersionDialogCommand.ExecuteAsync(model);

        CollectionAssert.AreEqual(filteredSearch ? new[] { "Pony" } : [], details.BaseModelFilter.ToArray());
    }

    private CivitCompatApiManager CompatApi() =>
        new(
            NullLogger<CivitCompatApiManager>.Instance,
            api,
            Substitute.For<ILykosModelDiscoveryApi>(),
            Substitute.For<ICivitTRPCApi>(),
            settings
        );

    private CivitDetailsPageViewModel Details(CivitModel model) =>
        new(
            settings,
            CompatApi(),
            Substitute.For<ICivitTRPCApi>(),
            NullLogger<CivitDetailsPageViewModel>.Instance,
            Substitute.For<INotificationService>(),
            Substitute.For<INavigationService<MainWindowViewModel>>(),
            modelIndex,
            factory,
            Substitute.For<IModelImportService>(),
            Substitute.For<IDownloadService>(),
            Substitute.For<IPackageFactory>(),
            Substitute.For<IWorkflowLibraryIndex>()
        )
        {
            CivitModel = model,
            ModelIdList = [1, 2],
            CurrentIndex = 0,
        };

    private CivitAiBrowserViewModel Browser() =>
        new(
            CompatApi(),
            settings,
            factory,
            Substitute.For<ILiteDbContext>(),
            Substitute.For<IConnectedServiceManager>(),
            Substitute.For<INotificationService>(),
            Substitute.For<ICivitBaseModelTypeService>(),
            Substitute.For<INavigationService<MainWindowViewModel>>(),
            Substitute.For<INavigationService<SettingsViewModel>>()
        );

    private CheckpointBrowserCardViewModel Card(CivitModel model) =>
        new(
            Substitute.For<IDownloadService>(),
            Substitute.For<ITrackedDownloadService>(),
            settings,
            factory,
            Substitute.For<INotificationService>(),
            modelIndex,
            Substitute.For<IModelImportService>(),
            Substitute.For<ILiteDbContext>(),
            CompatApi(),
            Substitute.For<INavigationService<MainWindowViewModel>>(),
            Substitute.For<IWorkflowLibraryIndex>()
        )
        {
            CivitModel = model,
        };

    private static CivitModel Model(int id, params CivitModelVersion[] versions) =>
        new()
        {
            Id = id,
            Name = $"Model {id}",
            Type = CivitModelType.Checkpoint,
            ModelVersions = versions.ToList(),
        };

    private static CivitModelVersion Version(int id, string baseModel) =>
        new()
        {
            Id = id,
            Name = $"Version {id}",
            BaseModel = baseModel,
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-id),
            Images = [],
            Files =
            [
                new CivitFile
                {
                    Id = id,
                    Type = CivitFileType.TrainingData,
                    Hashes = new CivitFileHashes(),
                },
            ],
        };

    private static Task LoadDetails(CivitDetailsPageViewModel vm) =>
        (Task)Invoke(vm, "OnInitialLoadedAsync")!;

    private static object? Invoke(object target, string method, params object?[] arguments) =>
        target
            .GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(target, arguments);

    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) => callback(state);
    }
}
