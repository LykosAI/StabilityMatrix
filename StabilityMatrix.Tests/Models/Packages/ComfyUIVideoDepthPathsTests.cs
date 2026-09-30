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
using YamlDotNet.RepresentationModel;

namespace StabilityMatrix.Tests.Models.Packages;

[TestClass]
public class ComfyUIVideoDepthPathsTests
{
    private string tempDir = null!;
    private string modelsDir = null!;
    private DirectoryPath installDir = null!;
    private ComfyUI comfy = null!;
    private ISettingsManager settings = null!;
    private string ConfigPath => installDir.JoinFile("extra_model_paths.yaml").FullPath;

    [TestInitialize]
    public void Initialize()
    {
        tempDir = Path.Combine(Path.GetTempPath(), $"sm-comfy-video-depth-{Guid.NewGuid():N}");
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
    public async Task Configuration_CreatesBothSharedDirectoriesAndCanonicalMappings()
    {
        await comfy.SetupModelFolders(installDir, SharedFolderMethod.Configuration);

        Assert.IsTrue(Directory.Exists(Path.Combine(modelsDir, "FrameInterpolation")));
        Assert.IsTrue(Directory.Exists(Path.Combine(modelsDir, "GeometryEstimation")));
        AssertMappings(ReadSection("stability_matrix"), modelsDir);
        Assert.AreEqual(
            Normalize(Path.Combine(modelsDir, "TextEncoders")),
            Scalar(ReadSection("stability_matrix"), "clip")
        );
    }

    [TestMethod]
    public async Task ImportedPackageAndRepeatedUpdates_RetainCustomSectionsAndExistingFiles()
    {
        var files = new Dictionary<string, string>();
        foreach (
            var (key, folder) in new[]
            {
                ("frame_interpolation", "FrameInterpolation"),
                ("geometry_estimation", "GeometryEstimation"),
            }
        )
        {
            var local = installDir.JoinDir("models", key);
            local.Create();
            var shared = Path.Combine(modelsDir, folder);
            Directory.CreateDirectory(shared);
            files[local.JoinFile("same.safetensors").FullPath] = $"local {key}";
            files[Path.Combine(shared, "same.safetensors")] = $"shared {key}";
        }
        foreach (var (path, content) in files)
            await File.WriteAllTextAsync(path, content);
        await File.WriteAllTextAsync(
            ConfigPath,
            """
            personal:
              base_path: /my/models
              frame_interpolation: custom/video
              geometry_estimation: custom/depth
              unet: custom/diffusion
            stability_matrix:
              clip: /old/TextEncoders
            """
        );

        await comfy.UpdateModelFolders(installDir, SharedFolderMethod.Configuration);
        var first = await File.ReadAllTextAsync(ConfigPath);
        await comfy.UpdateModelFolders(installDir, SharedFolderMethod.Configuration);

        Assert.AreEqual(first, await File.ReadAllTextAsync(ConfigPath));
        AssertMappings(ReadSection("stability_matrix"), modelsDir);
        var personal = ReadSection("personal");
        Assert.AreEqual("/my/models", Scalar(personal, "base_path"));
        Assert.AreEqual("custom/video", Scalar(personal, "frame_interpolation"));
        Assert.AreEqual("custom/depth", Scalar(personal, "geometry_estimation"));
        Assert.AreEqual("custom/diffusion", Scalar(personal, "unet"));
        foreach (var (path, content) in files)
            Assert.AreEqual(content, await File.ReadAllTextAsync(path));
        Assert.IsFalse(installDir.JoinDir("models", "frame_interpolation").IsSymbolicLink);
        Assert.IsFalse(installDir.JoinDir("models", "geometry_estimation").IsSymbolicLink);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ChangingLibraryRoot_ReplacesManagedPathsAndRetainsCustomSection(bool useMergeKey)
    {
        await File.WriteAllTextAsync(
            ConfigPath,
            "personal: &custom\n  frame_interpolation: /manual/video\n  geometry_estimation: /manual/depth\n"
                + (useMergeKey ? "stability_matrix:\n  <<: *custom\n" : "stability_matrix: {}\n")
        );
        await comfy.SetupModelFolders(installDir, SharedFolderMethod.Configuration);
        var oldFile = Path.Combine(modelsDir, "GeometryEstimation", "same.safetensors");
        await File.WriteAllTextAsync(oldFile, "old library model");

        foreach (var rootName in new[] { "NewModels", "NewestModels" })
        {
            modelsDir = Path.Combine(tempDir, rootName);
            settings.ModelsDirectory.Returns(modelsDir);
            await comfy.UpdateModelFolders(installDir, SharedFolderMethod.Configuration);

            AssertMappings(ReadSection("stability_matrix"), modelsDir);
            Assert.AreEqual("/manual/video", Scalar(ReadSection("personal"), "frame_interpolation"));
            Assert.AreEqual("/manual/depth", Scalar(ReadSection("personal"), "geometry_estimation"));
            Assert.AreEqual("old library model", await File.ReadAllTextAsync(oldFile));
        }
    }

    [TestMethod]
    public async Task Upgrade_DoesNotDiscoverPhysicalFoldersOrInventLoaderAliases()
    {
        foreach (
            var key in new[] { "depthanything", "rife", "moge", "photomaker", "optical_flow", "detection" }
        )
            installDir.JoinDir("models", key).Create();
        await File.WriteAllTextAsync(ConfigPath, "stability_matrix:\n  clip: /old/encoders\n");

        await comfy.UpdateModelFolders(installDir, SharedFolderMethod.Configuration);

        var managed = ReadSection("stability_matrix");
        AssertMappings(managed, modelsDir);
        foreach (
            var key in new[] { "depthanything", "rife", "moge", "photomaker", "optical_flow", "detection" }
        )
        {
            Assert.IsFalse(managed.Children.ContainsKey(key));
            Assert.IsFalse(Directory.Exists(Path.Combine(modelsDir, key)));
        }
    }

    [TestMethod]
    public async Task SharedRootLink_KeepsConfiguredPathsAndExistingFile()
    {
        var actualRoot = Path.Combine(tempDir, "ActualModels");
        Directory.CreateDirectory(Path.Combine(actualRoot, "FrameInterpolation"));
        var file = Path.Combine(actualRoot, "FrameInterpolation", "film.pth");
        await File.WriteAllTextAsync(file, "existing model");
        TempFiles.CreateDirectoryLink(modelsDir, actualRoot);

        await comfy.SetupModelFolders(installDir, SharedFolderMethod.Configuration);

        AssertMappings(ReadSection("stability_matrix"), modelsDir);
        Assert.AreEqual("existing model", await File.ReadAllTextAsync(file));
        Assert.IsTrue(new DirectoryPath(modelsDir).IsSymbolicLink);
    }

    [TestMethod]
    public async Task RemovingSharing_RetainsCustomSectionsAndModels()
    {
        await File.WriteAllTextAsync(ConfigPath, "personal:\n  geometry_estimation: /manual/depth\n");
        await comfy.SetupModelFolders(installDir, SharedFolderMethod.Configuration);
        var file = Path.Combine(modelsDir, "GeometryEstimation", "moge.pt");
        await File.WriteAllTextAsync(file, "existing model");

        await comfy.RemoveModelFolderLinks(installDir, SharedFolderMethod.Configuration);

        Assert.IsFalse(ReadRoot().Children.ContainsKey("stability_matrix"));
        Assert.AreEqual("/manual/depth", Scalar(ReadSection("personal"), "geometry_estimation"));
        Assert.AreEqual("existing model", await File.ReadAllTextAsync(file));
    }

    [TestMethod]
    public async Task SharingDisabled_DoesNotCreateDirectoriesOrRewriteYaml()
    {
        const string Yaml = "personal:\n  frame_interpolation: /manual/video\n";
        await File.WriteAllTextAsync(ConfigPath, Yaml);

        await comfy.SetupModelFolders(installDir, SharedFolderMethod.None);

        Assert.IsFalse(Directory.Exists(Path.Combine(modelsDir, "FrameInterpolation")));
        Assert.IsFalse(Directory.Exists(Path.Combine(modelsDir, "GeometryEstimation")));
        Assert.AreEqual(Yaml, await File.ReadAllTextAsync(ConfigPath));
    }

    [TestMethod]
    public void NewMappings_AreConfigurationOnlyWithDistinctFolderTypes()
    {
        Assert.IsFalse(comfy.SharedFolders!.ContainsKey(SharedFolderType.FrameInterpolation));
        Assert.IsFalse(comfy.SharedFolders.ContainsKey(SharedFolderType.GeometryEstimation));
        Assert.AreEqual("FrameInterpolation", SharedFolderType.FrameInterpolation.GetStringValue());
        Assert.AreEqual("GeometryEstimation", SharedFolderType.GeometryEstimation.GetStringValue());
        Assert.AreEqual(1ul << 37, (ulong)SharedFolderType.FrameInterpolation);
        Assert.AreEqual(1ul << 38, (ulong)SharedFolderType.GeometryEstimation);
        var values = Enum.GetValues<SharedFolderType>();
        Assert.AreEqual(values.Length, values.Distinct().Count());
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
        AssertMappings((YamlMappingNode)root.Children["stability_matrix"], sharedRoot);
    }

    private YamlMappingNode ReadRoot()
    {
        using var reader = File.OpenText(ConfigPath);
        var yaml = new YamlStream();
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    private YamlMappingNode ReadSection(string section) => (YamlMappingNode)ReadRoot().Children[section];

    private static void AssertMappings(YamlMappingNode node, string root)
    {
        Assert.AreEqual(
            Normalize(Path.Combine(root, "FrameInterpolation")),
            Scalar(node, "frame_interpolation")
        );
        Assert.AreEqual(
            Normalize(Path.Combine(root, "GeometryEstimation")),
            Scalar(node, "geometry_estimation")
        );
    }

    private static string Scalar(YamlMappingNode node, string key) =>
        ((YamlScalarNode)node.Children[key]).Value!;

    private static string Normalize(string path) => path.Replace('\\', '/');
}
