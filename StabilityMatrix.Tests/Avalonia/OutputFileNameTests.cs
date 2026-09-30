using System.Text;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class OutputFileNameTests
{
    private static FileNameFormat LongLoraFormat(string firstStem, string secondStem) =>
        FileNameFormat.Parse(
            "{lora_name}",
            new FileNameFormatProvider
            {
                GenerationLoras = GenerationLoraSnapshot.FromNodes(
                    [
                        new ComfyNode
                        {
                            ClassType = "LoraLoader",
                            Inputs = new() { ["lora_name"] = $"styles/{firstStem}.safetensors" },
                        },
                        new ComfyNode
                        {
                            ClassType = "LoraLoader",
                            Inputs = new() { ["lora_name"] = $"characters/{secondStem}.safetensors" },
                        },
                    ]
                ),
            }
        );

    [TestMethod]
    [DataRow("png", "")]
    [DataRow("webp", "_1")]
    [DataRow("mp4", "_100")]
    [DataRow("png", "_abcdef0")]
    public void LongMultipleLoras_FitAndCanBeSaved_WithGridBatchExtensionAndCollisionSuffixes(
        string extension,
        string collisionSuffix
    )
    {
        var format = LongLoraFormat(new string('a', 130), new string('b', 130));
        Assert.AreEqual(265, (format.GetFileName() + ".png").Length);
        format = format.WithGridPrefix().WithBatchPostFix(99, 100);
        var filename = format.GetOutputFileName(extension, collisionSuffix);

        Assert.IsTrue(filename.StartsWith("Grid_", StringComparison.Ordinal));
        Assert.IsTrue(filename.EndsWith($" (99-100){collisionSuffix}.{extension}", StringComparison.Ordinal));
        Assert.AreEqual(255, Encoding.UTF8.GetByteCount(filename));
        Assert.IsTrue(filename.Length <= 255);
        Assert.AreEqual(filename, format.GetOutputFileName(extension, collisionSuffix));
        AssertCanSave(filename);
    }

    [TestMethod]
    public void LongUnicodeLoras_FitUtf8AndUtf16Limits_WithoutSplittingSurrogates()
    {
        // Each source filename is valid individually under a 255-byte component limit.
        var format = LongLoraFormat(string.Concat(Enumerable.Repeat("🐺", 55)), new string('猫', 70));
        var filename = format.WithGridPrefix().WithBatchPostFix(1, 2).GetOutputFileName("webp", "_100");

        Assert.IsTrue(Encoding.UTF8.GetByteCount(filename) <= 255);
        Assert.IsTrue(filename.Length <= 255);
        Assert.IsFalse(filename.Contains('\uFFFD'));
        var strictUtf8 = new UTF8Encoding(false, true);
        Assert.AreEqual(filename, strictUtf8.GetString(strictUtf8.GetBytes(filename)));
        AssertCanSave(filename);
    }

    [TestMethod]
    public void DifferentLongCombinations_WithSameRetainedPrefix_HaveDifferentHashes()
    {
        var first = LongLoraFormat(new string('a', 130), new string('b', 130));
        var second = LongLoraFormat(new string('a', 130), new string('b', 129) + "c");
        var firstName = first.GetOutputFileName("png");
        var secondName = second.GetOutputFileName("png");

        Assert.AreEqual(firstName[..200], secondName[..200]);
        Assert.AreNotEqual(firstName, secondName);
    }

    [TestMethod]
    [DataRow(250, "", false)]
    [DataRow(251, "", false)]
    [DataRow(252, "", true)]
    [DataRow(247, "_100", false)]
    [DataRow(248, "_100", true)]
    public void ExactBoundary_IncludesExtensionAndCollisionSuffix(
        int stemLength,
        string suffix,
        bool shortened
    )
    {
        var stem = new string('a', stemLength);
        var format = FileNameFormat.Parse(stem, new FileNameFormatProvider());
        var name = format.GetOutputFileName("png", suffix);

        Assert.AreEqual(shortened, name != stem + suffix + ".png");
        Assert.IsTrue(Encoding.UTF8.GetByteCount(name) <= 255);
        Assert.IsTrue(name.EndsWith(suffix + ".png", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ShortNames_KeepFallbackGridBatchAndCollisionSpelling()
    {
        var format = FileNameFormat.Parse("{lora_name}", new FileNameFormatProvider());
        Assert.AreEqual("no_lora.png", format.GetOutputFileName("png"));
        Assert.AreEqual(
            "Grid_no_lora (1-2)_100.webp",
            format.WithGridPrefix().WithBatchPostFix(1, 2).GetOutputFileName("webp", "_100")
        );
    }

    [TestMethod]
    public void FolderPatterns_PreserveDirectories_AndBudgetOnlyTheFilename()
    {
        var format = FileNameFormat.Parse("outputs/" + new string('a', 300), new FileNameFormatProvider());
        var result = format.WithBatchPostFix(1, 2).GetOutputFileName("png", "_abcdef0");
        Assert.IsTrue(result.StartsWith("outputs/", StringComparison.Ordinal));
        Assert.AreEqual(255, Encoding.UTF8.GetByteCount(Path.GetFileName(result)));
    }

    [TestMethod]
    public void CollisionRetries_UseTheSameResolvedVariableValues()
    {
        var provider = new FileNameFormatProvider();
        var calls = 0;
        provider.Substitutions["time"] = () => (++calls).ToString();
        var format = FileNameFormat
            .Parse("{time}", provider)
            .WithGridPrefix()
            .WithBatchPostFix(1, 2)
            .WithResolvedVariables();

        Assert.AreEqual("Grid_1 (1-2).png", format.GetOutputFileName("png"));
        Assert.AreEqual("Grid_1 (1-2)_1.png", format.GetOutputFileName("png", "_1"));
        Assert.AreEqual("Grid_1 (1-2)_abcdef0.png", format.GetOutputFileName("png", "_abcdef0"));
        Assert.AreEqual(1, calls);
    }

    private static void AssertCanSave(string filename)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "sm-output-filename-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, filename);
        try
        {
            using (var stream = File.OpenWrite(path))
            {
                stream.WriteByte(42);
            }
            CollectionAssert.AreEqual(new byte[] { 42 }, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory);
        }
    }
}
