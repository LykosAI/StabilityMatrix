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
    public async Task SetCategories_WritesSidecarAndUpserts()
    {
        var model = CreateModel("foo");

        await service.SetCategoriesAsync(model, [" Character ", "Anime", "character"]);

        var sidecar = Path.Combine(root, "Lora", "foo.cm-user.json");
        Assert.IsTrue(File.Exists(sidecar));

        var written = UserModelMetadata.FromJson(await File.ReadAllTextAsync(sidecar));
        CollectionAssert.AreEqual(new[] { "Character", "Anime" }, written!.Categories);

        Assert.AreEqual(1, upserted.Count);
        CollectionAssert.AreEqual(new[] { "Character", "Anime" }, upserted[0].Categories.ToList());
        Assert.AreSame(model.RelativePath, upserted[0].RelativePath);
    }

    [TestMethod]
    public async Task SetCategories_Empty_DeletesSidecar()
    {
        var model = CreateModel("foo", "Character");
        var sidecar = Path.Combine(root, "Lora", "foo.cm-user.json");
        await File.WriteAllTextAsync(sidecar, model.UserMetadata!.ToJson());

        await service.SetCategoriesAsync(model, []);

        Assert.IsFalse(File.Exists(sidecar));
        Assert.AreEqual(1, upserted.Count);
        Assert.IsNull(upserted[0].UserMetadata);
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
        var sidecar = Path.Combine(root, "Lora", "a.cm-user.json");
        await File.WriteAllTextAsync(sidecar, only.UserMetadata!.ToJson());
        modelIndexService.ModelIndex.Returns(
            new Dictionary<SharedFolderType, List<LocalModelFile>> { [SharedFolderType.Lora] = [only, mixed] }
        );

        await service.DeleteCategoryAsync("style");

        Assert.AreEqual(0, settings.ModelCategories.Count);
        Assert.AreEqual(2, upserted.Count);
        Assert.IsFalse(File.Exists(sidecar), "sidecar with no categories left should be deleted");
        Assert.IsNull(upserted.Single(m => m.RelativePath == only.RelativePath).UserMetadata);
        CollectionAssert.AreEqual(
            new[] { "Anime" },
            upserted.Single(m => m.RelativePath == mixed.RelativePath).Categories.ToList()
        );
    }

    private static LocalModelFile CreateModel(string name, params string[] categories)
    {
        return new LocalModelFile
        {
            RelativePath = Path.Combine("Lora", $"{name}.safetensors"),
            SharedFolderType = SharedFolderType.Lora,
            UserMetadata =
                categories.Length == 0 ? null : new UserModelMetadata { Categories = [.. categories] },
        };
    }
}
