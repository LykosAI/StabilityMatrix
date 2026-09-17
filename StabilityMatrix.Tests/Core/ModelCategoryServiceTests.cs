using Microsoft.Extensions.Logging;
using NSubstitute;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class ModelCategoryServiceTests
{
    private string root = null!;
    private Settings settings = null!;
    private ISettingsManager settingsManager = null!;
    private IModelIndexService modelIndexService = null!;
    private List<LocalModelFile> upserted = null!;
    private ModelCategoryService service = null!;

    [TestInitialize]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "sm-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Lora"));

        settings = new Settings();
        settingsManager = Substitute.For<ISettingsManager>();
        settingsManager.Settings.Returns(settings);
        settingsManager.ModelsDirectory.Returns(root);
        settingsManager
            .When(x => x.Transaction(Arg.Any<Action<Settings>>(), Arg.Any<bool>()))
            .Do(call => call.Arg<Action<Settings>>()(settings));

        upserted = [];
        modelIndexService = Substitute.For<IModelIndexService>();
        modelIndexService
            .UpsertModelsAsync(Arg.Do<IEnumerable<LocalModelFile>>(models => upserted.AddRange(models)))
            .Returns(Task.CompletedTask);
        modelIndexService.ModelIndex.Returns(new Dictionary<SharedFolderType, List<LocalModelFile>>());
        modelIndexService.ModelIndexCategories.Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        service = new ModelCategoryService(
            settingsManager,
            modelIndexService,
            Substitute.For<ILogger<ModelCategoryService>>()
        );
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(root, true);
    }

    [TestMethod]
    public async Task SetCategories_BareModel_WritesCategoryOnlySidecar()
    {
        var model = CreateModel("foo");

        await service.SetCategoriesAsync(model, [" Character ", "Anime", "character"]);

        var onDisk = await ReadSidecar("foo");
        CollectionAssert.AreEqual(new[] { "Character", "Anime" }, onDisk.User!.Categories);
        Assert.IsFalse(onDisk.IsConnected, "a category-only sidecar must not look connected");
        Assert.IsNull(onDisk.Source);

        Assert.AreEqual(1, upserted.Count);
        CollectionAssert.AreEqual(new[] { "Character", "Anime" }, upserted[0].Categories.ToList());
        Assert.IsFalse(upserted[0].HasConnectedModel);
    }

    [TestMethod]
    public async Task SetCategories_ConnectedModel_KeepsRemoteFields()
    {
        var model = CreateModel(
            "foo",
            remote: new ConnectedModelInfo
            {
                ModelId = 42,
                ModelName = "Remote",
                BaseModel = "Pony",
            }
        );
        await model.ConnectedModelInfo!.SaveJsonToDirectory(Path.Combine(root, "Lora"), "foo");

        await service.SetCategoriesAsync(model, ["Style"]);

        var onDisk = await ReadSidecar("foo");
        Assert.AreEqual(42, onDisk.ModelId);
        Assert.AreEqual("Remote", onDisk.ModelName);
        Assert.AreEqual("Pony", onDisk.BaseModel);
        CollectionAssert.AreEqual(new[] { "Style" }, onDisk.User!.Categories);

        Assert.IsTrue(upserted[0].HasConnectedModel);
        Assert.AreNotSame(
            model.ConnectedModelInfo,
            upserted[0].ConnectedModelInfo,
            "index instance must not be mutated"
        );
        Assert.AreEqual(0, model.Categories.Count, "index instance must not be mutated");
    }

    [TestMethod]
    public async Task SetCategories_Empty_DeletesCategoryOnlySidecar()
    {
        var model = CreateModel("foo", "Character");
        await model.ConnectedModelInfo!.SaveJsonToDirectory(Path.Combine(root, "Lora"), "foo");

        await service.SetCategoriesAsync(model, []);

        Assert.IsFalse(File.Exists(SidecarPath("foo")));
        Assert.IsNull(upserted[0].ConnectedModelInfo);
    }

    [TestMethod]
    public async Task SetCategories_Empty_KeepsSidecarWithCustomMetadataOrOptOut()
    {
        var custom = CreateModel(
            "a",
            remote: new ConnectedModelInfo
            {
                ModelName = "My name",
                User = new UserModelMetadata { Categories = ["X"] },
            }
        );
        var optedOut = CreateModel(
            "b",
            remote: new ConnectedModelInfo
            {
                Source = null,
                User = new UserModelMetadata { Categories = ["X"], ExcludeFromMetadataSearch = true },
            }
        );

        await service.SetCategoriesAsync(custom, []);
        await service.SetCategoriesAsync(optedOut, []);

        Assert.AreEqual("My name", (await ReadSidecar("a")).ModelName);
        Assert.IsTrue((await ReadSidecar("b")).User!.ExcludeFromMetadataSearch);
        Assert.AreEqual(0, (await ReadSidecar("b")).User!.Categories.Count);
    }

    [TestMethod]
    public async Task AddToCategory_SkipsModelsThatAlreadyHaveIt()
    {
        var tagged = CreateModel("a", "Style");
        var untagged = CreateModel("b");

        await service.AddToCategoryAsync([tagged, untagged], "style");

        Assert.AreEqual(1, upserted.Count);
        Assert.AreEqual(untagged.RelativePath, upserted[0].RelativePath);
        CollectionAssert.AreEqual(new[] { "style" }, upserted[0].Categories.ToList());
    }

    [TestMethod]
    public async Task RemoveFromCategory_OnlyTouchesModelsThatHaveIt()
    {
        var both = CreateModel("a", "Style", "Anime");
        var other = CreateModel("b", "Anime");

        await service.RemoveFromCategoryAsync([both, other], "STYLE");

        Assert.AreEqual(1, upserted.Count);
        CollectionAssert.AreEqual(new[] { "Anime" }, upserted[0].Categories.ToList());
    }

    [TestMethod]
    public void AddCategory_RejectsCaseInsensitiveDuplicate()
    {
        Assert.IsTrue(service.AddCategory("Character", "#D9930D"));
        Assert.IsFalse(service.AddCategory("character"));

        Assert.AreEqual(1, settings.ModelCategories.Count);
        Assert.AreEqual("#D9930D", settings.ModelCategories[0].Color);
    }

    [TestMethod]
    public void GetCategories_AppendsUnregisteredNamesFromIndex()
    {
        service.AddCategory("Character");
        modelIndexService.ModelIndexCategories.Returns(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "character", "Zeta", "Alpha" }
        );

        var result = service.GetCategories().Select(c => c.Name).ToList();

        CollectionAssert.AreEqual(new[] { "Character", "Alpha", "Zeta" }, result);
    }

    [TestMethod]
    public async Task RenameCategory_UpdatesRegistryAndEveryAffectedModel()
    {
        service.AddCategory("Charcter");
        var affected = CreateModel("a", "Charcter", "Anime");
        var unaffected = CreateModel("b", "Anime");
        modelIndexService.ModelIndex.Returns(
            new Dictionary<SharedFolderType, List<LocalModelFile>>
            {
                [SharedFolderType.Lora] = [affected, unaffected],
            }
        );

        await service.RenameCategoryAsync("charcter", "Character");

        Assert.AreEqual("Character", settings.ModelCategories.Single().Name);
        Assert.AreEqual(1, upserted.Count);
        CollectionAssert.AreEqual(new[] { "Character", "Anime" }, upserted[0].Categories.ToList());
    }

    [TestMethod]
    public async Task RenameCategory_OntoExisting_MergesWithoutDuplicates()
    {
        service.AddCategory("Anime");
        service.AddCategory("Animu");
        var model = CreateModel("a", "Animu", "Anime");
        modelIndexService.ModelIndex.Returns(
            new Dictionary<SharedFolderType, List<LocalModelFile>> { [SharedFolderType.Lora] = [model] }
        );

        await service.RenameCategoryAsync("Animu", "Anime");

        Assert.AreEqual(1, settings.ModelCategories.Count);
        CollectionAssert.AreEqual(new[] { "Anime" }, upserted.Single().Categories.ToList());
    }

    [TestMethod]
    public async Task DeleteCategory_RemovesFromRegistryAndModels()
    {
        service.AddCategory("Style");
        var only = CreateModel("a", "Style");
        var mixed = CreateModel("b", "Style", "Anime");
        await only.ConnectedModelInfo!.SaveJsonToDirectory(Path.Combine(root, "Lora"), "a");
        modelIndexService.ModelIndex.Returns(
            new Dictionary<SharedFolderType, List<LocalModelFile>> { [SharedFolderType.Lora] = [only, mixed] }
        );

        await service.DeleteCategoryAsync("style");

        Assert.AreEqual(0, settings.ModelCategories.Count);
        Assert.AreEqual(2, upserted.Count);
        Assert.IsFalse(File.Exists(SidecarPath("a")), "sidecar with nothing left should be deleted");
        Assert.IsNull(upserted.Single(m => m.RelativePath == only.RelativePath).ConnectedModelInfo);
        CollectionAssert.AreEqual(
            new[] { "Anime" },
            upserted.Single(m => m.RelativePath == mixed.RelativePath).Categories.ToList()
        );
    }

    private string SidecarPath(string name) => Path.Combine(root, "Lora", name + ".cm-info.json");

    private async Task<ConnectedModelInfo> ReadSidecar(string name)
    {
        Assert.IsTrue(File.Exists(SidecarPath(name)), $"{name} sidecar missing");
        return ConnectedModelInfo.FromJson(await File.ReadAllTextAsync(SidecarPath(name)))!;
    }

    private static LocalModelFile CreateModel(string name, params string[] categories) =>
        CreateModel(
            name,
            categories.Length == 0
                ? null
                : new ConnectedModelInfo
                {
                    Source = null,
                    User = new UserModelMetadata { Categories = [.. categories] },
                }
        );

    private static LocalModelFile CreateModel(string name, ConnectedModelInfo? remote)
    {
        return new LocalModelFile
        {
            RelativePath = Path.Combine("Lora", $"{name}.safetensors"),
            SharedFolderType = SharedFolderType.Lora,
            ConnectedModelInfo = remote,
        };
    }
}
