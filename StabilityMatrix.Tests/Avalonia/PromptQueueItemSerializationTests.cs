using System.Text.Json;
using System.Text.Json.Nodes;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Models.Inference;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class PromptQueueItemSerializationTests
{
    private static PromptQueueItem CreateSampleItem() =>
        new()
        {
            Name = "Sample - 12:00 PM",
            ProjectType = InferenceProjectType.TextToImage,
            Status = PromptQueueItemStatus.Completed,
            Progress = 42,
            ProgressText = "sampling",
            ErrorMessage = null,
            PromptSnippet = "a cat sitting on a windowsill, masterpiece",
            ParamsSummary = "1024×1024 · 20 steps · seed 1234",
            OutputImagePaths = { "C:/images/a.png", "C:/images/b.png" },
            Project = new InferenceProjectDocument
            {
                ProjectType = InferenceProjectType.TextToImage,
                State = new JsonObject { ["Seed"] = new JsonObject { ["Seed"] = 1234 } },
            },
        };

    [TestMethod]
    public void PromptQueueItem_RoundTrips_PreservesPersistedFields()
    {
        var item = CreateSampleItem();

        var json = JsonSerializer.Serialize(item);
        var restored = JsonSerializer.Deserialize<PromptQueueItem>(json);

        Assert.IsNotNull(restored);
        Assert.AreEqual(item.Id, restored.Id);
        Assert.AreEqual(item.Name, restored.Name);
        Assert.AreEqual(item.ProjectType, restored.ProjectType);
        Assert.AreEqual(item.Status, restored.Status);
        Assert.AreEqual(item.Progress, restored.Progress);
        Assert.AreEqual(item.ProgressText, restored.ProgressText);
        Assert.AreEqual(item.PromptSnippet, restored.PromptSnippet);
        Assert.AreEqual(item.ParamsSummary, restored.ParamsSummary);
        CollectionAssert.AreEqual(item.OutputImagePaths, restored.OutputImagePaths);

        Assert.IsNotNull(restored.Project.State);
        Assert.AreEqual(1234, restored.Project.State!["Seed"]!["Seed"]!.GetValue<int>());
    }

    [TestMethod]
    public void PromptQueueItem_Status_SerializesAsString()
    {
        var item = CreateSampleItem();
        item.Status = PromptQueueItemStatus.Failed;

        var json = JsonSerializer.Serialize(item);

        StringAssert.Contains(json, "\"Failed\"");
    }

    [TestMethod]
    public void PromptQueueItem_PreviewImage_IsNotSerialized()
    {
        var item = CreateSampleItem();

        var json = JsonSerializer.Serialize(item);

        Assert.IsFalse(json.Contains("PreviewImage"), "Preview image must never be serialized");
    }

    [TestMethod]
    public void PromptQueueItem_ListOfItems_RoundTrips()
    {
        var items = new List<PromptQueueItem> { CreateSampleItem(), CreateSampleItem() };

        var json = JsonSerializer.Serialize(items);
        var restored = JsonSerializer.Deserialize<List<PromptQueueItem>>(json);

        Assert.IsNotNull(restored);
        Assert.AreEqual(2, restored.Count);
    }
}
