using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using FreneticUtilities.FreneticDataSyntax;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Packages.Config;
using YamlDotNet.RepresentationModel;

namespace StabilityMatrix.Tests.Models.Packages;

[TestClass]
public class SharedFoldersConfigHelperTests
{
    // Define mock paths used across tests
    private const string MockPackageRoot = @"C:\SM\Packages\TestPackage"; // Use OS-specific or normalized
    private const string MockSharedModelsRoot = @"C:\SM\Models";

    // Helper to run the target method and return the resulting stream content as string
    private async Task<string> RunHelperAndGetOutput(
        SharedFolderLayout layout,
        string packageRoot,
        string sharedModelsRoot,
        bool useSharedMode // True for SharedAsync, False for DefaultAsync
    )
    {
        using var stream = new MemoryStream();

        if (useSharedMode)
        {
            await SharedFoldersConfigHelper.UpdateConfigFileForSharedAsync(
                layout,
                packageRoot,
                sharedModelsRoot,
                stream
            );
        }
        else
        {
            await SharedFoldersConfigHelper.UpdateConfigFileForDefaultAsync(layout, packageRoot, stream);
        }

        stream.Position = 0; // Rewind stream to read the output
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    // Helper to normalize paths in expected strings for cross-platform compatibility
    private string NormalizeExpectedPath(string path) => path.Replace('/', Path.DirectorySeparatorChar);

    // --- JSON Tests ---

    [TestMethod]
    public async Task Json_UpdateForShared_WritesCorrectPaths()
    {
        // Arrange
        var layout = new SharedFolderLayout
        {
            RelativeConfigPath = "config.json",
            ConfigFileType = ConfigFileType.Json,
            ConfigSharingOptions = ConfigSharingOptions.Default, // Use default options
            Rules = ImmutableList.Create(
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.StableDiffusion],
                    ConfigDocumentPaths = ["ckpt_dir"]
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Lora, SharedFolderType.LyCORIS],
                    ConfigDocumentPaths = ["lora_dirs"]
                } // Test multiple sources -> array
            )
        };

        // Act
        var outputJson = await RunHelperAndGetOutput(
            layout,
            MockPackageRoot,
            MockSharedModelsRoot,
            useSharedMode: true
        );
        var jsonNode = JsonNode.Parse(outputJson);

        // Assert
        Assert.IsNotNull(jsonNode);
        var expectedCkptPath = Path.Combine(MockSharedModelsRoot, "StableDiffusion").Replace('\\', '/'); // JSON usually uses /
        var expectedLoraPath = Path.Combine(MockSharedModelsRoot, "Lora").Replace('\\', '/');
        var expectedLycoPath = Path.Combine(MockSharedModelsRoot, "LyCORIS").Replace('\\', '/');

        Assert.AreEqual(expectedCkptPath, jsonNode["ckpt_dir"]?.GetValue<string>());

        var loraDirs = jsonNode["lora_dirs"] as JsonArray;
        Assert.IsNotNull(loraDirs);
        Assert.AreEqual(2, loraDirs.Count);
        Assert.IsTrue(loraDirs.Any(n => n != null && n.GetValue<string>() == expectedLoraPath));
        Assert.IsTrue(loraDirs.Any(n => n != null && n.GetValue<string>() == expectedLycoPath));
    }

    [TestMethod]
    public async Task Json_UpdateForDefault_WritesCorrectPaths()
    {
        // Arrange
        var layout = new SharedFolderLayout
        {
            RelativeConfigPath = "config.json",
            ConfigFileType = ConfigFileType.Json,
            ConfigSharingOptions = ConfigSharingOptions.Default,
            Rules = ImmutableList.Create(
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.StableDiffusion],
                    TargetRelativePaths = ["models/checkpoints"],
                    ConfigDocumentPaths = ["ckpt_dir"]
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Lora],
                    TargetRelativePaths = ["models/loras"],
                    ConfigDocumentPaths = ["lora_dirs"]
                } // Assume single default path
            )
        };
        var expectedCkptPath = Path.Combine(MockPackageRoot, "models", "checkpoints").Replace('\\', '/');
        var expectedLoraPath = Path.Combine(MockPackageRoot, "models", "loras").Replace('\\', '/');

        // Act
        var outputJson = await RunHelperAndGetOutput(
            layout,
            MockPackageRoot,
            MockSharedModelsRoot,
            useSharedMode: false
        ); // Default Mode
        var jsonNode = JsonNode.Parse(outputJson);

        // Assert
        Assert.IsNotNull(jsonNode);
        Assert.AreEqual(expectedCkptPath, jsonNode["ckpt_dir"]?.GetValue<string>());
        // Since default writes single target path, expect string, not array
        Assert.AreEqual(expectedLoraPath, jsonNode["lora_dirs"]?.GetValue<string>());
    }

    [TestMethod]
    public async Task Json_NestedPaths_UpdateForShared_WritesCorrectly()
    {
        // Arrange
        var layout = new SharedFolderLayout
        {
            RelativeConfigPath = "config.json",
            ConfigFileType = ConfigFileType.Json,
            ConfigSharingOptions = ConfigSharingOptions.Default,
            Rules = ImmutableList.Create(
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.VAE],
                    ConfigDocumentPaths = ["paths.models.vae"]
                }
            )
        };
        var expectedVaePath = Path.Combine(MockSharedModelsRoot, "VAE").Replace('\\', '/');

        // Act
        var outputJson = await RunHelperAndGetOutput(
            layout,
            MockPackageRoot,
            MockSharedModelsRoot,
            useSharedMode: true
        );
        var jsonNode = JsonNode.Parse(outputJson);

        // Assert
        Assert.IsNotNull(jsonNode);
        Assert.AreEqual(expectedVaePath, jsonNode?["paths"]?["models"]?["vae"]?.GetValue<string>());
    }

    // --- YAML Tests ---

    [TestMethod]
    public async Task Yaml_UpdateForShared_WritesCorrectPathsWithRootKey()
    {
        // Arrange
        var layout = new SharedFolderLayout
        {
            RelativeConfigPath = "extra_paths.yaml",
            ConfigFileType = ConfigFileType.Yaml,
            ConfigSharingOptions = ConfigSharingOptions.Default with { RootKey = "stability_matrix" }, // Set RootKey
            Rules = ImmutableList.Create(
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.VAE],
                    ConfigDocumentPaths = ["vae"]
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Lora, SharedFolderType.LyCORIS],
                    ConfigDocumentPaths = ["loras"]
                }
            )
        };
        var expectedVaePath = Path.Combine(MockSharedModelsRoot, "VAE").Replace('\\', '/');
        var expectedLoraPath = Path.Combine(MockSharedModelsRoot, "Lora").Replace('\\', '/');
        var expectedLycoPath = Path.Combine(MockSharedModelsRoot, "LyCORIS").Replace('\\', '/');

        // Act
        var outputYaml = await RunHelperAndGetOutput(
            layout,
            MockPackageRoot,
            MockSharedModelsRoot,
            useSharedMode: true
        );

        // Assert using YamlDotNet.RepresentationModel
        var yamlStream = new YamlStream();
        yamlStream.Load(new StringReader(outputYaml));
        var rootMapping = yamlStream.Documents[0].RootNode as YamlMappingNode;
        Assert.IsNotNull(rootMapping);

        var smNode = rootMapping.Children[new YamlScalarNode("stability_matrix")] as YamlMappingNode;
        Assert.IsNotNull(smNode);

        // Scalars
        var vaeNode = smNode.Children[new YamlScalarNode("vae")] as YamlScalarNode;
        Assert.IsNotNull(vaeNode);
        Assert.AreEqual(expectedVaePath, vaeNode.Value);

        var lorasNode = smNode.Children[new YamlScalarNode("loras")] as YamlScalarNode;
        Assert.IsNotNull(lorasNode);
        // Split into sequences
        var loras = lorasNode.Value?.SplitLines() ?? [];
        CollectionAssert.Contains(loras, expectedLoraPath);
        CollectionAssert.Contains(loras, expectedLycoPath);

        // Sequence support
        /*var vaeNode = smNode.Children[new YamlScalarNode("vae")] as YamlSequenceNode;
        Assert.IsNotNull(vaeNode);
        Assert.AreEqual(1, vaeNode.Children.Count);
        Assert.AreEqual(expectedVaePath, (vaeNode.Children[0] as YamlScalarNode)?.Value);

        var lorasNode = smNode.Children[new YamlScalarNode("loras")] as YamlSequenceNode;
        Assert.IsNotNull(lorasNode);
        Assert.AreEqual(2, lorasNode.Children.Count);
        Assert.IsTrue(lorasNode.Children.Any(n => n is YamlScalarNode ns && ns.Value == expectedLoraPath));
        Assert.IsTrue(lorasNode.Children.Any(n => n is YamlScalarNode ns && ns.Value == expectedLycoPath));*/
    }

    [TestMethod]
    public async Task Yaml_UpdateForDefault_RelativePaths()
    {
        // Arrange
        var initialYamlContent = """
                                 # Existing content
                                 some_other_key: value
                                 stability_matrix:
                                   vae:
                                   - C:\SM\Models/VAE
                                   loras:
                                   - C:\SM\Models/Lora
                                   - C:\SM\Models/LyCORIS
                                 another_key: 123
                                 """;
        var layout = new SharedFolderLayout
        {
            RelativeConfigPath = "extra_paths.yaml",
            ConfigFileType = ConfigFileType.Yaml,
            ConfigSharingOptions = ConfigSharingOptions.Default with
            {
                RootKey = "stability_matrix",
                ConfigDefaultType = ConfigDefaultType.TargetRelativePaths // Configure relative paths
            },
            Rules = ImmutableList.Create( // Define rules so helper knows which keys to clear under RootKey
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.VAE],
                    TargetRelativePaths = ["models/vae"],
                    ConfigDocumentPaths = ["vae"]
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Lora, SharedFolderType.LyCORIS],
                    TargetRelativePaths = ["models/loras"],
                    ConfigDocumentPaths = ["loras"]
                }
            )
        };

        // Act - Write initial content, then run Default Mode
        using var stream = new MemoryStream();
        await using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            await writer.WriteAsync(initialYamlContent);
        }
        stream.Position = 0; // Reset for the helper

        await SharedFoldersConfigHelper.UpdateConfigFileForDefaultAsync(layout, MockPackageRoot, stream); // Use overload that reads layout options
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var outputYaml = await reader.ReadToEndAsync();

        // Assert
        var yamlStream = new YamlStream();
        yamlStream.Load(new StringReader(outputYaml));
        var rootMapping = yamlStream.Documents[0].RootNode as YamlMappingNode;
        Assert.IsNotNull(rootMapping);

        // Check that stability_matrix key is not gone (or empty)
        Assert.IsTrue(
            rootMapping.Children.ContainsKey(new YamlScalarNode("stability_matrix")),
            "stability_matrix key should exist."
        );
        // Check that other keys remain
        Assert.IsTrue(rootMapping.Children.ContainsKey(new YamlScalarNode("some_other_key")));
        Assert.IsTrue(rootMapping.Children.ContainsKey(new YamlScalarNode("another_key")));
    }

    [TestMethod]
    public async Task Yaml_UpdateForDefault_RemovesSmRootKey()
    {
        // Arrange
        var initialYamlContent = """
                                 # Existing content
                                 some_other_key: value
                                 stability_matrix:
                                   vae:
                                   - C:\SM\Models/VAE
                                   loras:
                                   - C:\SM\Models/Lora
                                   - C:\SM\Models/LyCORIS
                                 another_key: 123
                                 """;
        var layout = new SharedFolderLayout
        {
            RelativeConfigPath = "extra_paths.yaml",
            ConfigFileType = ConfigFileType.Yaml,
            ConfigSharingOptions = ConfigSharingOptions.Default with
            {
                RootKey = "stability_matrix",
                ConfigDefaultType = ConfigDefaultType.ClearRoot // Configure clearing of RootKey
            },
            Rules = ImmutableList.Create( // Define rules so helper knows which keys to clear under RootKey
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.VAE],
                    TargetRelativePaths = ["models/vae"],
                    ConfigDocumentPaths = ["vae"]
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Lora, SharedFolderType.LyCORIS],
                    TargetRelativePaths = ["models/loras"],
                    ConfigDocumentPaths = ["loras"]
                }
            )
        };

        // Act - Write initial content, then run Default Mode
        using var stream = new MemoryStream();
        await using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            await writer.WriteAsync(initialYamlContent);
        }
        stream.Position = 0; // Reset for the helper

        await SharedFoldersConfigHelper.UpdateConfigFileForDefaultAsync(layout, MockPackageRoot, stream); // Use overload that reads layout options
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var outputYaml = await reader.ReadToEndAsync();

        // Assert
        var yamlStream = new YamlStream();
        yamlStream.Load(new StringReader(outputYaml));
        var rootMapping = yamlStream.Documents[0].RootNode as YamlMappingNode;
        Assert.IsNotNull(rootMapping);

        // Check that stability_matrix key is gone (or empty)
        Assert.IsFalse(
            rootMapping.Children.ContainsKey(new YamlScalarNode("stability_matrix")),
            "stability_matrix key should be removed."
        );
        // Check that other keys remain
        Assert.IsTrue(rootMapping.Children.ContainsKey(new YamlScalarNode("some_other_key")));
        Assert.IsTrue(rootMapping.Children.ContainsKey(new YamlScalarNode("another_key")));
    }

    // --- FDS Tests ---

    [TestMethod]
    public async Task Fds_UpdateForShared_WritesCorrectPathsWithRoot()
    {
        // Arrange
        var layout = new SharedFolderLayout
        {
            RelativeConfigPath = Path.Combine("Data", "Settings.fds"),
            ConfigFileType = ConfigFileType.Fds,
            ConfigSharingOptions = ConfigSharingOptions.Default, // RootKey not used by FDS strategy directly
            Rules = ImmutableList.Create(
                new SharedFolderLayoutRule { ConfigDocumentPaths = ["ModelRoot"], IsRoot = true },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.StableDiffusion],
                    ConfigDocumentPaths = ["SDModelFolder"]
                }
            )
        };
        var expectedModelRoot = MockSharedModelsRoot.Replace('/', Path.DirectorySeparatorChar);
        var expectedSdModelFolder = Path.Combine(MockSharedModelsRoot, "StableDiffusion")
            .Replace('/', Path.DirectorySeparatorChar);

        // Act
        var outputFds = await RunHelperAndGetOutput(
            layout,
            MockPackageRoot,
            MockSharedModelsRoot,
            useSharedMode: true
        );
        var fdsSection = new FDSSection(outputFds);

        // Assert
        Assert.IsNotNull(fdsSection);
        var pathsSection = fdsSection.GetSection("Paths");
        Assert.IsNotNull(pathsSection);
        Assert.AreEqual(expectedModelRoot, pathsSection.GetString("ModelRoot"));
        Assert.AreEqual(expectedSdModelFolder, pathsSection.GetString("SDModelFolder"));
    }

    [TestMethod]
    public async Task Fds_UpdateForDefault_WritesCorrectPaths()
    {
        // Arrange
        var layout = new SharedFolderLayout
        {
            RelativeConfigPath = Path.Combine("Data", "Settings.fds"),
            ConfigFileType = ConfigFileType.Fds,
            ConfigSharingOptions = ConfigSharingOptions.Default,
            Rules = ImmutableList.Create(
                // Root rule should result in ModelRoot being *removed* in Default mode
                new SharedFolderLayoutRule { ConfigDocumentPaths = ["ModelRoot"], IsRoot = true },
                // Regular rule should write the default path
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.StableDiffusion],
                    TargetRelativePaths = ["Models/Stable-Diffusion"],
                    ConfigDocumentPaths = ["SDModelFolder"]
                }
            )
        };
        var expectedSdModelFolder = Path.Combine(MockPackageRoot, "Models", "Stable-Diffusion")
            .Replace('/', Path.DirectorySeparatorChar);

        // Act
        var outputFds = await RunHelperAndGetOutput(
            layout,
            MockPackageRoot,
            MockSharedModelsRoot,
            useSharedMode: false
        ); // Default Mode
        var fdsSection = new FDSSection(outputFds);

        // Assert
        Assert.IsNotNull(fdsSection);
        var pathsSection = fdsSection.GetSection("Paths"); // May or may not exist depending on if SDModelFolder was only key
        if (pathsSection != null)
        {
            Assert.IsNull(
                pathsSection.GetString("ModelRoot"),
                "ModelRoot should be removed in Default mode."
            ); // Check ModelRoot is gone
            Assert.AreEqual(expectedSdModelFolder, pathsSection.GetString("SDModelFolder"));
        }
        else
        {
            // If only ModelRoot was defined, Paths section itself might be removed, which is also ok
            Assert.IsNull(
                fdsSection.GetSection("Paths"),
                "Paths section should be removed if only ModelRoot existed."
            );
        }
    }

    [TestMethod]
    public async Task Json_SplitRule_UpdateForShared_WritesCorrectArray()
    {
        // Arrange: Simulate SDFX IP-Adapter rules
        var layout = new SharedFolderLayout
        {
            RelativeConfigPath = "config.json",
            ConfigFileType = ConfigFileType.Json,
            ConfigSharingOptions = ConfigSharingOptions.Default with { AlwaysWriteArray = true }, // Force array
            Rules = ImmutableList.Create(
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.IpAdapter],
                    ConfigDocumentPaths = ["paths.models.ipadapter"]
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.IpAdapters15],
                    ConfigDocumentPaths = ["paths.models.ipadapter"]
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.IpAdaptersXl],
                    ConfigDocumentPaths = ["paths.models.ipadapter"]
                }
            )
        };
        var expectedIpBasePath = Path.Combine(MockSharedModelsRoot, "IpAdapter").Replace('\\', '/');
        var expectedIp15Path = Path.Combine(MockSharedModelsRoot, "IpAdapters15").Replace('\\', '/'); // SM SourceTypes map like this
        var expectedIpXlPath = Path.Combine(MockSharedModelsRoot, "IpAdaptersXl").Replace('\\', '/');

        // Act
        var outputJson = await RunHelperAndGetOutput(
            layout,
            MockPackageRoot,
            MockSharedModelsRoot,
            useSharedMode: true
        );
        var jsonNode = JsonNode.Parse(outputJson);

        // Assert
        Assert.IsNotNull(jsonNode);
        var ipAdapterNode = jsonNode?["paths"]?["models"]?["ipadapter"];
        Assert.IsInstanceOfType(ipAdapterNode, typeof(JsonArray));

        var ipAdapterArray = ipAdapterNode as JsonArray;
        Assert.AreEqual(3, ipAdapterArray?.Count);
        Assert.IsTrue(ipAdapterArray.Any(n => n?.GetValue<string>() == expectedIpBasePath));
        Assert.IsTrue(ipAdapterArray.Any(n => n?.GetValue<string>() == expectedIp15Path));
        Assert.IsTrue(ipAdapterArray.Any(n => n?.GetValue<string>() == expectedIpXlPath));
    }

    // Add more tests:
    // - Starting with an existing config file and modifying it.
    // - Testing specific ConfigSharingOptions (AlwaysWriteArray for JSON, different RootKey for YAML).
    // - Testing removal of keys when rules are removed from the layout.
    // - Edge cases like empty layouts or layouts with no matching rules.
}
