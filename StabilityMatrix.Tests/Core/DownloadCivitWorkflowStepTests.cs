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
