using NSubstitute;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Services.Rocm;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace StabilityMatrix.Tests.Models.Packages;

[TestClass]
public class ComfyUISharedModelPathsTests
{
    private string tempDir = null!;
    private string modelsDir = null!;
    private DirectoryPath installDir = null!;
    private ComfyUI comfy = null!;
    private ISettingsManager settings = null!;
    private string ConfigPath => installDir.JoinFile("extra_model_paths.yaml").FullPath;
    private string LatentDir => Path.Combine(modelsDir, "LatentUpscaleModels");

    [TestInitialize]
    public void Initialize()
    {
        tempDir = Path.Combine(Path.GetTempPath(), $"sm-comfy-paths-{Guid.NewGuid():N}");
        modelsDir = Path.Combine(tempDir, "Models with spaces 日本語");
        installDir = new DirectoryPath(tempDir, "Packages", "ComfyUI");
        installDir.Create();
        settings = Substitute.For<ISettingsManager>();
        settings.ModelsDirectory.Returns(modelsDir);
        settings.WorkflowDirectory.Returns(new DirectoryPath(tempDir, "Workflows"));
        comfy = new ComfyUI(
            Substitute.For<IGithubApiCache>(),
            settings,
            Substitute.For<IDownloadService>(),
            Substitute.For<IPrerequisiteHelper>(),
            Substitute.For<IPyInstallationManager>(),
            Substitute.For<IPipWheelService>(),
            Substitute.For<IRocmPackageHelper>()
        );
    }

    [TestCleanup]
    public void Cleanup() => TempFiles.DeleteDirectory(tempDir);

    [TestMethod]
    public async Task Configuration_CreatesSupportedMappingAndSharedDirectory()
    {
        await comfy.SetupModelFolders(installDir, SharedFolderMethod.Configuration);

        Assert.IsTrue(Directory.Exists(LatentDir));
        var paths = ReadSection("stability_matrix");
        Assert.AreEqual(Normalize(LatentDir), Scalar(paths, "latent_upscale_models"));
        Assert.AreEqual(Normalize(Path.Combine(modelsDir, "TextEncoders")), Scalar(paths, "clip"));
        Assert.AreEqual(
            Normalize(Path.Combine(modelsDir, "DiffusionModels")),
            Scalar(paths, "diffusion_models")
        );
        Assert.AreEqual(
            string.Join(
                "\n",
                new[] { "ESRGAN", "RealESRGAN", "SwinIR" }.Select(p => Normalize(Path.Combine(modelsDir, p)))
            ),
            Scalar(paths, "upscale_models")
        );
        Assert.IsFalse(paths.Children.ContainsKey("depthanything"));
        Assert.IsFalse(paths.Children.ContainsKey("unet_gguf"));
        Assert.IsFalse(paths.Children.ContainsKey("custom_nodes"));
        Assert.IsFalse(paths.Children.ContainsKey("configs"));
    }

    [TestMethod]
    public async Task ImportedPackageAndRepeatedUpdates_PreserveFilesAndCustomPaths()
    {
        var localDir = installDir.JoinDir("models", "latent_upscale_models");
        localDir.Create();
        Directory.CreateDirectory(LatentDir);
        var localFile = localDir.JoinFile("same.safetensors").FullPath;
        var sharedFile = Path.Combine(LatentDir, "same.safetensors");
        await File.WriteAllTextAsync(localFile, "local model");
        await File.WriteAllTextAsync(sharedFile, "shared model");
        await File.WriteAllTextAsync(
            ConfigPath,
            """
            personal:
              base_path: /my/models
              latent_upscale_models: custom/latent
              unet: custom/diffusion
            stability_matrix:
              latent_upscale_models: /manual/latent
              private_category: /manual/custom
              text_encoders: /manual/encoders
            """
        );

        await comfy.UpdateModelFolders(installDir, SharedFolderMethod.Configuration);
        var first = await File.ReadAllTextAsync(ConfigPath);
        await comfy.UpdateModelFolders(installDir, SharedFolderMethod.Configuration);

        Assert.AreEqual(first, await File.ReadAllTextAsync(ConfigPath));
        Assert.AreEqual("local model", await File.ReadAllTextAsync(localFile));
        Assert.AreEqual("shared model", await File.ReadAllTextAsync(sharedFile));
        Assert.IsFalse(localDir.IsSymbolicLink);
        Assert.AreEqual("custom/latent", Scalar(ReadSection("personal"), "latent_upscale_models"));
        Assert.AreEqual("custom/diffusion", Scalar(ReadSection("personal"), "unet"));
        var paths = ReadSection("stability_matrix");
        Assert.AreEqual(Normalize(LatentDir), Scalar(paths, "latent_upscale_models"));
        Assert.AreEqual("/manual/custom", Scalar(paths, "private_category"));
        Assert.AreEqual("/manual/encoders", Scalar(paths, "text_encoders"));
    }

    [TestMethod]
    public async Task UpgradeFromOlderYaml_AddsMappingWithoutScanningPhysicalFolders()
    {
        installDir.JoinDir("models", "depthanything").Create();
        installDir.JoinDir("models", "clip").Create();
        await File.WriteAllTextAsync(ConfigPath, "stability_matrix:\n  clip: /old/TextEncoders\n");

        await comfy.UpdateModelFolders(installDir, SharedFolderMethod.Configuration);

        Assert.AreEqual(
            Normalize(LatentDir),
            Scalar(ReadSection("stability_matrix"), "latent_upscale_models")
        );
        Assert.IsFalse(Directory.Exists(Path.Combine(modelsDir, "depthanything")));
        Assert.IsFalse(Directory.Exists(Path.Combine(modelsDir, "clip")));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ChangingLibraryRoot_ReplacesManagedPathsAndRetainsCustomSection(bool useMergeKey)
    {
        await File.WriteAllTextAsync(
            ConfigPath,
            "personal: &custom\n  latent_upscale_models: /manual/latent\n  checkpoints: /manual/checkpoints\n"
                + (
                    useMergeKey
                        ? "stability_matrix:\n  <<: *custom\n"
                        : "stability_matrix:\n  latent_upscale_models: /manual/latent\n"
                )
        );
        await comfy.SetupModelFolders(installDir, SharedFolderMethod.Configuration);
        var originalModel = Path.Combine(LatentDir, "same.safetensors");
        await File.WriteAllTextAsync(originalModel, "old library model");

        foreach (var rootName in new[] { "NewModels", "NewestModels" })
        {
            modelsDir = Path.Combine(tempDir, rootName);
            settings.ModelsDirectory.Returns(modelsDir);
            Directory.CreateDirectory(LatentDir);
            var currentModel = Path.Combine(LatentDir, "same.safetensors");
            await File.WriteAllTextAsync(currentModel, rootName);

            await comfy.UpdateModelFolders(installDir, SharedFolderMethod.Configuration);

            var managed = ReadSection("stability_matrix");
            Assert.AreEqual(Normalize(LatentDir), Scalar(managed, "latent_upscale_models"));
            Assert.AreEqual(
                Normalize(Path.Combine(modelsDir, "StableDiffusion")),
                Scalar(managed, "checkpoints")
            );
            Assert.AreEqual("/manual/latent", Scalar(ReadSection("personal"), "latent_upscale_models"));
            Assert.AreEqual("/manual/checkpoints", Scalar(ReadSection("personal"), "checkpoints"));
            Assert.AreEqual("old library model", await File.ReadAllTextAsync(originalModel));
            Assert.AreEqual(rootName, await File.ReadAllTextAsync(currentModel));
        }
    }

    [TestMethod]
    public async Task Configuration_SharedRootLinkKeepsConfiguredPathAndExistingFiles()
    {
        var actualRoot = Path.Combine(tempDir, "ActualModels");
        Directory.CreateDirectory(Path.Combine(actualRoot, "LatentUpscaleModels"));
        var model = Path.Combine(actualRoot, "LatentUpscaleModels", "existing.safetensors");
        await File.WriteAllTextAsync(model, "existing shared model");
        TempFiles.CreateDirectoryLink(modelsDir, actualRoot);

        await comfy.SetupModelFolders(installDir, SharedFolderMethod.Configuration);

        Assert.AreEqual(
            Normalize(LatentDir),
            Scalar(ReadSection("stability_matrix"), "latent_upscale_models")
        );
        Assert.AreEqual("existing shared model", await File.ReadAllTextAsync(model));
        Assert.IsTrue(new DirectoryPath(modelsDir).IsSymbolicLink);
    }

    [TestMethod]
    public async Task RemovingConfigurationSharing_PreservesOtherSectionsAndModelFiles()
    {
        Directory.CreateDirectory(LatentDir);
        var model = Path.Combine(LatentDir, "existing.safetensors");
        await File.WriteAllTextAsync(model, "existing shared model");
        await File.WriteAllTextAsync(ConfigPath, "personal:\n  latent_upscale_models: /manual\n");
        await comfy.SetupModelFolders(installDir, SharedFolderMethod.Configuration);

        await comfy.RemoveModelFolderLinks(installDir, SharedFolderMethod.Configuration);

        Assert.AreEqual("/manual", Scalar(ReadSection("personal"), "latent_upscale_models"));
        Assert.AreEqual("existing shared model", await File.ReadAllTextAsync(model));
        using var reader = File.OpenText(ConfigPath);
        var yaml = new YamlStream();
        yaml.Load(reader);
        Assert.IsFalse(
            ((YamlMappingNode)yaml.Documents[0].RootNode).Children.ContainsKey("stability_matrix")
        );
    }

    [TestMethod]
    public void LatentMapping_IsConfigurationOnlyAndHasUniqueFolderType()
    {
        Assert.IsFalse(comfy.SharedFolders!.ContainsKey(SharedFolderType.LatentUpscaleModels));
        Assert.AreEqual("LatentUpscaleModels", SharedFolderType.LatentUpscaleModels.GetStringValue());
        Assert.AreEqual(1ul << 36, (ulong)SharedFolderType.LatentUpscaleModels);
    }

    [TestMethod]
    public async Task SharingDisabled_DoesNotCreateOrChangeModelPaths()
    {
        await File.WriteAllTextAsync(ConfigPath, "personal:\n  latent_upscale_models: /manual\n");
        await comfy.SetupModelFolders(installDir, SharedFolderMethod.None);

        Assert.IsFalse(Directory.Exists(LatentDir));
        Assert.AreEqual(
            "personal:\n  latent_upscale_models: /manual\n",
            await File.ReadAllTextAsync(ConfigPath)
        );
    }

    [TestMethod]
    [DataRow("stability_matrix: [broken")]
    [DataRow("- a sequence")]
    [DataRow("stability_matrix: /manual")]
    [DataRow("personal: {}\n---\nother: {}")]
    public async Task InvalidExistingYaml_IsNotOverwritten(string yaml)
    {
        await File.WriteAllTextAsync(ConfigPath, yaml);
        var original = await File.ReadAllBytesAsync(ConfigPath);

        try
        {
            await comfy.SetupModelFolders(installDir, SharedFolderMethod.Configuration);
            Assert.Fail("Invalid YAML should reject the configuration update.");
        }
        catch (YamlException)
        {
            CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(ConfigPath));
        }
    }

    [TestMethod]
    [DataRow("C:\\Model Library\\日本語")]
    [DataRow("\\\\server\\share\\Models")]
    [DataRow("/mnt/model library/日本語")]
    public async Task GeneratedPaths_UseForwardSlashesAndYamlScalars(string sharedRoot)
    {
        using var stream = new MemoryStream();
        await SharedFoldersConfigHelper.UpdateConfigFileForSharedAsync(
            comfy.SharedFolderLayout,
            installDir.FullPath,
            sharedRoot,
            stream
        );
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var yaml = new YamlStream();
        yaml.Load(reader);
        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        var paths = (YamlMappingNode)root.Children["stability_matrix"];
        Assert.AreEqual(
            Normalize(Path.Combine(sharedRoot, "LatentUpscaleModels")),
            Scalar(paths, "latent_upscale_models")
        );
    }

    private YamlMappingNode ReadSection(string section)
    {
        using var reader = File.OpenText(ConfigPath);
        var yaml = new YamlStream();
        yaml.Load(reader);
        return (YamlMappingNode)((YamlMappingNode)yaml.Documents[0].RootNode).Children[section];
    }

    private static string Scalar(YamlMappingNode node, string key) =>
        ((YamlScalarNode)node.Children[key]).Value!;

    private static string Normalize(string path) => path.Replace('\\', '/');
}
