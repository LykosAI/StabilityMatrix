using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Inference;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class FileNameFormatTests
{
    [TestMethod]
    public void TestFileNameFormatParse()
    {
        var provider = new FileNameFormatProvider
        {
            GenerationParameters = new GenerationParameters { Seed = 123 },
            ProjectName = "uwu",
            ProjectType = InferenceProjectType.TextToImage,
        };

        var format = FileNameFormat.Parse("{project_type} - {project_name} ({seed})", provider);

        Assert.AreEqual("TextToImage - uwu (123)", format.GetFileName());
    }
}
