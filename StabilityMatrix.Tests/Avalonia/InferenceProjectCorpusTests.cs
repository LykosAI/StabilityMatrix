using System.Text.Json;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Inference;

namespace StabilityMatrix.Tests.Avalonia;

/// <summary>
/// Opens projects saved by earlier releases, in <c>TestData/InferenceProjects/{version}</c>. Each one
/// must queue the same ComfyUI prompt as its <see cref="InferenceProjectScenario"/> applied to a new tab.
/// </summary>
[TestClass]
public class InferenceProjectCorpusTests
{
    /// <summary>Folder <see cref="WriteCorpus"/> writes to when set, such as <c>TestData/InferenceProjects/2.17.0</c>.</summary>
    private const string WriteCorpusVariable = "SM_WRITE_PROJECT_CORPUS";

    private static readonly JsonSerializerOptions ProjectFileOptions = new() { WriteIndented = true };

    private static InferenceTestHost host = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context) => host = new InferenceTestHost();

    [ClassCleanup]
    public static void ClassCleanup() => host?.Dispose();

    [TestInitialize]
    public void TestInitialize()
    {
        // View models observe on the current SynchronizationContext when constructed
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
    }

    public static IEnumerable<object[]> ProjectFiles()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "TestData", "InferenceProjects");
        return Directory
            .EnumerateFiles(root, "*.smproj", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(file => new object[] { Path.GetRelativePath(root, file).Replace('\\', '/') });
    }

    [TestMethod]
    [DynamicData(nameof(ProjectFiles), DynamicDataSourceType.Method)]
    public void SavedProject_QueuesSamePromptAsNewTab(string file)
    {
        var scenario = InferenceProjectScenario.All.Single(s =>
            s.Name == Path.GetFileNameWithoutExtension(file)
        );
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "InferenceProjects", file);

        var document = JsonSerializer.Deserialize<InferenceProjectDocument>(File.ReadAllText(path))!;
        document.VerifyVersion();
        Assert.AreEqual(scenario.ProjectType, document.ProjectType);

        var loaded = (InferenceTabViewModelBase)host.Create(document.ProjectType.ToViewModelType()!);
        loaded.LoadStateFromJsonObject(document.State!);
        InferenceProjectScenario.UseInputImage(loaded, host);

        Assert.AreEqual(Prompt(NewTab(scenario)), Prompt(loaded));
    }

    /// <summary>
    /// Writes every scenario as a project file, the way Inference saves one, to the folder in
    /// <c>SM_WRITE_PROJECT_CORPUS</c>. Run it on each release that changes what a project stores.
    /// </summary>
    [TestMethod]
    public void WriteCorpus()
    {
        var folder = Environment.GetEnvironmentVariable(WriteCorpusVariable);
        if (string.IsNullOrEmpty(folder))
        {
            Assert.Inconclusive(
                $"Set {WriteCorpusVariable} to a folder to write this version's project files"
            );
        }

        Directory.CreateDirectory(folder);
        foreach (var scenario in InferenceProjectScenario.All)
        {
            var document = InferenceProjectDocument.FromLoadable(NewTab(scenario));
            var json = JsonSerializer.Serialize(document, ProjectFileOptions);

            // The input image is wherever the writing machine put it
            json = json.Replace(JsonEncodedText.Encode(host.InputImagePath).ToString(), "input.png")
                .Replace(host.InputImagePath, "input.png");

            File.WriteAllText(Path.Combine(folder, $"{scenario.Name}.smproj"), json);
        }
    }

    private static InferenceTabViewModelBase NewTab(InferenceProjectScenario scenario)
    {
        var tab = (InferenceTabViewModelBase)host.Create(scenario.ProjectType.ToViewModelType()!);
        scenario.Apply(tab, host);
        InferenceProjectScenario.UseInputImage(tab, host);
        return tab;
    }

    /// <summary>The prompt as <see cref="ComfyClient"/> would post it.</summary>
    private static string Prompt(InferenceTabViewModelBase tab) =>
        JsonSerializer.Serialize(
            InferenceTestHost.BuildNodes((InferenceGenerationViewModelBase)tab),
            new JsonSerializerOptions(ComfyClient.SerializerOptions) { WriteIndented = true }
        );
}
