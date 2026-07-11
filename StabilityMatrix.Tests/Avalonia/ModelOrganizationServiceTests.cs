using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class ModelOrganizationServiceTests
{
    private readonly ModelOrganizationService service = new();

    [TestMethod]
    public void BuildPlan_UsesLocalMetadataPattern()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            var scopePath = Path.Combine(tempRoot, "Checkpoints");
            var model = CreateModelFile(
                tempRoot,
                Path.Combine("Checkpoints", "Source", "local-file.safetensors"),
                "remote-file.safetensors",
                authorUsername: "creator-name",
                baseModel: "SDXL"
            );

            var plan = service.BuildPlan(
                [model],
                tempRoot,
                scopePath,
                includeNested: true,
                template: "{author}/{base_model}/{file_name}"
            );

            Assert.AreEqual(1, plan.Items.Count);
            Assert.AreEqual(1, plan.ReadyCount);
            Assert.AreEqual(
                Path.Combine(scopePath, "creator-name", "SDXL", "remote-file.safetensors"),
                plan.Items[0].TargetPath
            );
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public void BuildPlan_SkipsUnsupportedVariable()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            var scopePath = Path.Combine(tempRoot, "Checkpoints");
            var model = CreateModelFile(
                tempRoot,
                Path.Combine("Checkpoints", "local-file.safetensors"),
                "remote-file.safetensors"
            );

            var plan = service.BuildPlan(
                [model],
                tempRoot,
                scopePath,
                includeNested: true,
                template: "{seed}"
            );

            Assert.AreEqual(1, plan.Items.Count);
            Assert.AreEqual(0, plan.ReadyCount);
            StringAssert.Contains(plan.Items[0].Reason, "not supported");
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public void BuildPlan_RespectsNestedScope()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            var scopePath = Path.Combine(tempRoot, "Checkpoints", "Group");
            var model = CreateModelFile(
                tempRoot,
                Path.Combine("Checkpoints", "Group", "Nested", "local-file.safetensors"),
                "remote-file.safetensors"
            );

            var withoutNested = service.BuildPlan(
                [model],
                tempRoot,
                scopePath,
                includeNested: false,
                template: "{file_name}"
            );
            var withNested = service.BuildPlan(
                [model],
                tempRoot,
                scopePath,
                includeNested: true,
                template: "{file_name}"
            );

            Assert.AreEqual(0, withoutNested.Items.Count);
            Assert.AreEqual(1, withNested.Items.Count);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public async Task ApplyPlan_MovesModelAndSidecars()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            var scopePath = Path.Combine(tempRoot, "Checkpoints");
            var relativePath = Path.Combine("Checkpoints", "Source", "local-file.safetensors");
            var model = CreateModelFile(tempRoot, relativePath, "remote-file.safetensors");
            var sourcePath = Path.Combine(tempRoot, relativePath);
            await File.WriteAllTextAsync(
                Path.Combine(Path.GetDirectoryName(sourcePath)!, "local-file.cm-info.json"),
                "{}"
            );
            await File.WriteAllTextAsync(
                Path.Combine(Path.GetDirectoryName(sourcePath)!, "local-file.preview.png"),
                "preview"
            );
            await File.WriteAllTextAsync(
                Path.Combine(Path.GetDirectoryName(sourcePath)!, "local-file.yaml"),
                "config"
            );

            var plan = service.BuildPlan(
                [model],
                tempRoot,
                scopePath,
                includeNested: true,
                template: "organized/{file_name}"
            );

            var result = await service.ApplyPlan(plan);

            var targetDirectory = Path.Combine(scopePath, "organized");
            Assert.AreEqual(1, result.MovedCount);
            Assert.IsTrue(File.Exists(Path.Combine(targetDirectory, "remote-file.safetensors")));
            Assert.IsTrue(File.Exists(Path.Combine(targetDirectory, "remote-file.cm-info.json")));
            Assert.IsTrue(File.Exists(Path.Combine(targetDirectory, "remote-file.preview.png")));
            Assert.IsTrue(File.Exists(Path.Combine(targetDirectory, "remote-file.yaml")));
            Assert.IsFalse(File.Exists(sourcePath));
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public void BuildPlan_PreservesTypeFolderWhenScopeIsRoot()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            // scopePath is the models root, not a type folder
            var scopePath = tempRoot;
            var loraModel = CreateModelFile(
                tempRoot,
                Path.Combine("Lora", "SD 1.5", "add_detail.safetensors"),
                "add_detail.safetensors",
                authorUsername: "creator",
                baseModel: "SD 1.5"
            );
            var checkpointModel = CreateModelFile(
                tempRoot,
                Path.Combine("StableDiffusion", "some_model.safetensors"),
                "some_model.safetensors",
                authorUsername: "creator",
                baseModel: "SDXL"
            );

            var plan = service.BuildPlan(
                [loraModel, checkpointModel],
                tempRoot,
                scopePath,
                includeNested: true,
                template: "{base_model}/{file_name}"
            );

            var loraItem = plan.Items.First(i => i.SourcePath.Contains("Lora"));
            var checkpointItem = plan.Items.First(i => i.SourcePath.Contains("StableDiffusion"));

            // Lora should stay within the Lora type folder
            Assert.IsTrue(
                loraItem.TargetPath!.Contains(Path.Combine("Lora", "SD 1.5")),
                $"Lora target should stay in Lora folder, got: {loraItem.TargetPath}"
            );

            // Checkpoint should stay within the StableDiffusion type folder
            Assert.IsTrue(
                checkpointItem.TargetPath!.Contains(Path.Combine("StableDiffusion", "SDXL")),
                $"Checkpoint target should stay in StableDiffusion folder, got: {checkpointItem.TargetPath}"
            );
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public void BuildPlan_PreservesDotsInFileName()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            var scopePath = Path.Combine(tempRoot, "Checkpoints");
            var model = CreateModelFile(
                tempRoot,
                Path.Combine("Checkpoints", "local-file.safetensors"),
                "wan2.1_i2v_480p_14B_fp8_e4m3fn.safetensors"
            );

            var plan = service.BuildPlan(
                [model],
                tempRoot,
                scopePath,
                includeNested: true,
                template: "{file_name}"
            );

            Assert.AreEqual(1, plan.Items.Count);
            Assert.AreEqual(1, plan.ReadyCount);
            Assert.AreEqual(
                Path.Combine(scopePath, "wan2.1_i2v_480p_14B_fp8_e4m3fn.safetensors"),
                plan.Items[0].TargetPath
            );
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public void BuildPlan_PreservesSelectedNestedScopeForMultiSegmentTemplates()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            var scopePath = Path.Combine(tempRoot, "StableDiffusion", "Favorites");
            var model = CreateModelFile(
                tempRoot,
                Path.Combine("StableDiffusion", "Favorites", "local-file.safetensors"),
                "remote-file.safetensors",
                baseModel: "SDXL"
            );

            var plan = service.BuildPlan(
                [model],
                tempRoot,
                scopePath,
                includeNested: true,
                template: "{base_model}/{file_name}"
            );

            Assert.AreEqual(1, plan.Items.Count);
            Assert.AreEqual(
                Path.Combine(scopePath, "SDXL", "remote-file.safetensors"),
                plan.Items[0].TargetPath
            );
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public async Task ApplyPlan_RollsBackCompletedMovesWhenLaterMoveFails()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            var scopePath = Path.Combine(tempRoot, "Checkpoints");
            var relativePath = Path.Combine("Checkpoints", "Source", "local-file.safetensors");
            var model = CreateModelFile(tempRoot, relativePath, "remote-file.safetensors");
            var sourcePath = Path.Combine(tempRoot, relativePath);
            var sourceDirectory = Path.GetDirectoryName(sourcePath)!;
            var cmInfoPath = Path.Combine(sourceDirectory, "local-file.cm-info.json");
            var previewPath = Path.Combine(sourceDirectory, "local-file.preview.png");

            await File.WriteAllTextAsync(cmInfoPath, "{}");
            await File.WriteAllTextAsync(previewPath, "preview");

            var plan = service.BuildPlan(
                [model],
                tempRoot,
                scopePath,
                includeNested: true,
                template: "organized/{file_name}"
            );

            File.Delete(previewPath);

            var result = await service.ApplyPlan(plan);

            var targetDirectory = Path.Combine(scopePath, "organized");
            Assert.AreEqual(0, result.MovedCount);
            Assert.AreEqual(1, result.SkippedCount);
            Assert.AreEqual(1, result.Errors.Count);
            Assert.IsTrue(File.Exists(sourcePath));
            Assert.IsTrue(File.Exists(cmInfoPath));
            Assert.IsFalse(File.Exists(Path.Combine(targetDirectory, "remote-file.safetensors")));
            Assert.IsFalse(File.Exists(Path.Combine(targetDirectory, "remote-file.cm-info.json")));
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public async Task MoveModelFileAsync_MovesModelAndSidecars()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            var model = CreateModelFile(
                tempRoot,
                Path.Combine("StableDiffusion", "z_image_turbo.safetensors"),
                "z_image_turbo.safetensors"
            );

            // Sidecars next to the model
            var sourceDir = Path.Combine(tempRoot, "StableDiffusion");
            File.WriteAllText(Path.Combine(sourceDir, "z_image_turbo.cm-info.json"), "{}");
            File.WriteAllText(Path.Combine(sourceDir, "z_image_turbo.preview.jpeg"), "img");

            var destinationDir = Path.Combine(tempRoot, "DiffusionModels");

            await service.MoveModelFileAsync(model, tempRoot, destinationDir);

            Assert.IsTrue(File.Exists(Path.Combine(destinationDir, "z_image_turbo.safetensors")));
            Assert.IsTrue(File.Exists(Path.Combine(destinationDir, "z_image_turbo.cm-info.json")));
            Assert.IsTrue(File.Exists(Path.Combine(destinationDir, "z_image_turbo.preview.jpeg")));
            Assert.IsFalse(File.Exists(Path.Combine(sourceDir, "z_image_turbo.safetensors")));
            Assert.IsFalse(File.Exists(Path.Combine(sourceDir, "z_image_turbo.cm-info.json")));
            Assert.IsFalse(File.Exists(Path.Combine(sourceDir, "z_image_turbo.preview.jpeg")));
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public async Task MoveModelFileAsync_DestinationExists_ThrowsWithoutMoving()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            var model = CreateModelFile(
                tempRoot,
                Path.Combine("StableDiffusion", "model.safetensors"),
                "model.safetensors"
            );

            var destinationDir = Path.Combine(tempRoot, "DiffusionModels");
            Directory.CreateDirectory(destinationDir);
            File.WriteAllText(Path.Combine(destinationDir, "model.safetensors"), "existing");

            await Assert.ThrowsExceptionAsync<FileTransferExistsException>(() =>
                service.MoveModelFileAsync(model, tempRoot, destinationDir)
            );

            // Source must be untouched
            Assert.IsTrue(File.Exists(Path.Combine(tempRoot, "StableDiffusion", "model.safetensors")));
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    private static LocalModelFile CreateModelFile(
        string root,
        string relativePath,
        string remoteFileName,
        string? authorUsername = null,
        string? baseModel = null
    )
    {
        var fullPath = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "model");

        return new LocalModelFile
        {
            RelativePath = relativePath,
            SharedFolderType = SharedFolderType.StableDiffusion,
            ConnectedModelInfo = new ConnectedModelInfo
            {
                ModelId = 123,
                ModelName = "Remote Model",
                VersionId = 456,
                VersionName = "Version One",
                ModelType = CivitModelType.Checkpoint,
                Hashes = new CivitFileHashes { BLAKE3 = "hash" },
                AuthorUsername = authorUsername,
                BaseModel = baseModel,
                RemoteFileName = remoteFileName,
                RemoteFileId = 321,
                Source = ConnectedModelSource.Civitai,
            },
        };
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sm-organizer-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
