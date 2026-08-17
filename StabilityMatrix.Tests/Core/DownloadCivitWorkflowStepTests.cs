using System.IO.Compression;
using System.Text.Json.Nodes;
using NSubstitute;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class DownloadCivitWorkflowStepTests
{
    private const string WorkflowJson = """{"nodes": [], "links": []}""";

    private string tempDir = null!;
    private ISettingsManager settingsManager = null!;
    private IDownloadService downloadService = null!;

    private static CivitModel TestModel =>
        new()
        {
            Id = 123,
            Name = "Test Workflow Pack",
            Type = CivitModelType.Workflows,
            Creator = new CivitCreator { Username = "tester" },
            Stats = new CivitModelStats { DownloadCount = 5, ThumbsUpCount = 2 },
        };

    private static CivitModelVersion TestVersion =>
        new()
        {
            Id = 456,
            Name = "v1.0",
            Images =
            [
                new CivitImage
                {
                    Url = "https://example.com/preview.jpg",
                    Type = "image",
                    Width = 512,
                    Height = 512,
                },
            ],
        };

    [TestInitialize]
    public void Initialize()
    {
        tempDir = Path.Combine(Path.GetTempPath(), $"sm-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        settingsManager = Substitute.For<ISettingsManager>();
        settingsManager.WorkflowDirectory.Returns(new DirectoryPath(tempDir, "Workflows"));

        downloadService = Substitute.For<IDownloadService>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
    }

    [TestMethod]
    public async Task ImportsWorkflowJsonsFromZip_WithMetadataEmbedded()
    {
        SetupDownload(
            BuildZip(
                ("workflow-a.json", WorkflowJson),
                ("nested/workflow-b.json", WorkflowJson),
                ("preview.png", "not json"),
                ("__MACOSX/workflow-a.json", WorkflowJson)
            )
        );

        await CreateStep(fileName: "pack.zip").ExecuteAsync();

        var importedFiles = Directory
            .EnumerateFiles(tempDir, "*.json", SearchOption.AllDirectories)
            .OrderBy(f => f)
            .ToList();

        Assert.AreEqual(2, importedFiles.Count);
        Assert.IsTrue(importedFiles.Any(f => f.EndsWith("workflow-a.json")));
        Assert.IsTrue(importedFiles.Any(f => f.EndsWith("workflow-b.json")));
        StringAssert.Contains(importedFiles[0], "Test Workflow Pack");

        var metadataIds = new HashSet<string>();
        foreach (var file in importedFiles)
        {
            var root = JsonNode.Parse(await File.ReadAllTextAsync(file))!.AsObject();

            // Original workflow content is preserved
            Assert.IsNotNull(root["nodes"]);

            var workflowData = root["sm_workflow_data"]!.AsObject();
            StringAssert.Contains(workflowData["name"]!.GetValue<string>(), "Test Workflow Pack");
            Assert.AreEqual("tester", workflowData["creator"]!["username"]!.GetValue<string>());
            Assert.AreEqual(5, workflowData["stats"]!["num_downloads"]!.GetValue<int>());
            Assert.AreEqual(
                "https://example.com/preview.jpg",
                workflowData["thumbnails"]![0]!["url"]!.GetValue<string>()
            );
            Assert.AreEqual(
                "https://civitai.com/models/123?modelVersionId=456",
                root["sm_source_url"]!.GetValue<string>()
            );

            metadataIds.Add(workflowData["id"]!.GetValue<string>());
        }

        // Each file gets a unique library id so the installed workflows cache never collides
        Assert.AreEqual(2, metadataIds.Count);
    }

    [TestMethod]
    public async Task ImportsBareWorkflowJson()
    {
        SetupDownload(System.Text.Encoding.UTF8.GetBytes(WorkflowJson));

        await CreateStep(fileName: "single-workflow.json").ExecuteAsync();

        var importedFile = Directory.EnumerateFiles(tempDir, "*.json", SearchOption.AllDirectories).Single();

        var root = JsonNode.Parse(await File.ReadAllTextAsync(importedFile))!.AsObject();
        Assert.AreEqual("Test Workflow Pack", root["sm_workflow_data"]!["name"]!.GetValue<string>());
    }

    [TestMethod]
    public async Task ThrowsWhenArchiveContainsNoWorkflows()
    {
        SetupDownload(BuildZip(("readme.txt", "no workflows here")));

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            CreateStep(fileName: "empty.zip").ExecuteAsync()
        );
    }

    [TestMethod]
    public async Task ImportsWorkflowEmbeddedInPng_KeepingImageAsPreview()
    {
        SetupDownloadBytes(BuildPngWithWorkflow(WorkflowJson));

        await CreateStep(fileName: "workflow-image.png").ExecuteAsync();

        var importedJson = Directory.EnumerateFiles(tempDir, "*.json", SearchOption.AllDirectories).Single();

        var root = JsonNode.Parse(await File.ReadAllTextAsync(importedJson))!.AsObject();
        Assert.IsNotNull(root["nodes"]);
        Assert.IsNotNull(root["sm_workflow_data"]);

        // The image is kept as the workflow's preview sidecar
        var sidecar = Path.Combine(Path.GetDirectoryName(importedJson)!, "workflow-image.preview.png");
        Assert.IsTrue(File.Exists(sidecar));
    }

    [TestMethod]
    public async Task ExplainsWebUiParameterImages_WhenNothingImportable()
    {
        // A1111/Forge showcase renders carry only a "parameters" chunk - no workflow
        SetupDownloadBytes(BuildPngWithChunk("parameters", "masterpiece, steps: 20"));

        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            CreateStep(fileName: "showcase.png").ExecuteAsync()
        );

        StringAssert.Contains(exception.Message, "generation parameters");
    }

    [TestMethod]
    public async Task ImportsPngsFromZip_SkippingImagesWithoutEmbeddedWorkflow()
    {
        SetupDownloadBytes(
            BuildZipBytes(
                ("with-workflow.png", BuildPngWithWorkflow(WorkflowJson)),
                ("plain-image.png", BuildPngWithWorkflow(null))
            )
        );

        await CreateStep(fileName: "pngs.zip").ExecuteAsync();

        var importedFiles = Directory.EnumerateFiles(tempDir, "*.json", SearchOption.AllDirectories).ToList();

        Assert.AreEqual(1, importedFiles.Count);
        Assert.IsTrue(importedFiles[0].EndsWith("with-workflow.json"));
    }

    private DownloadCivitWorkflowStep CreateStep(string fileName) =>
        new(
            TestModel,
            TestVersion,
            new CivitFile
            {
                Id = 789,
                Name = fileName,
                DownloadUrl = "https://civitai.com/api/download/models/456",
            },
            downloadService,
            settingsManager
        );

    private void SetupDownloadBytes(byte[] content) => SetupDownload(content);

    private static byte[] BuildPngWithWorkflow(string? workflowJson) =>
        workflowJson is null ? BuildPngWithChunk(null, null) : BuildPngWithChunk("workflow", workflowJson);

    /// <summary>
    /// Minimal PNG: signature + optional tEXt chunk + IEND. Chunk CRCs are
    /// zeroed - the metadata reader walks chunks without validating them.
    /// </summary>
    private static byte[] BuildPngWithChunk(string? keyword, string? text)
    {
        using var stream = new MemoryStream();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        void WriteChunk(string type, byte[] data)
        {
            var length = BitConverter.GetBytes(data.Length);
            Array.Reverse(length); // big-endian
            stream.Write(length);
            stream.Write(System.Text.Encoding.ASCII.GetBytes(type));
            stream.Write(data);
            stream.Write(new byte[4]); // crc, unvalidated
        }

        if (keyword is not null)
        {
            WriteChunk("tEXt", System.Text.Encoding.UTF8.GetBytes($"{keyword}\0{text}"));
        }

        WriteChunk("IEND", []);

        return stream.ToArray();
    }

    private static byte[] BuildZipBytes(params (string Name, byte[] Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var entryStream = entry.Open();
                entryStream.Write(content);
            }
        }

        return stream.ToArray();
    }

    private void SetupDownload(byte[] content)
    {
        downloadService
            .DownloadToFileAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IProgress<ProgressReport>?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(callInfo =>
            {
                File.WriteAllBytes(callInfo.ArgAt<string>(1), content);
                return Task.CompletedTask;
            });
    }

    private static byte[] BuildZip(params (string Name, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }
}
