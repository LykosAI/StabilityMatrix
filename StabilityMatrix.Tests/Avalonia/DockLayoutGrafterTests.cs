using Avalonia.Collections;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Core;
using Dock.Serializer;
using StabilityMatrix.Avalonia.Controls.Dock;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class DockLayoutGrafterTests
{
    private static readonly DockSerializer Serializer = new(typeof(AvaloniaList<>));

    private static Tool CreateTool(string id) =>
        new()
        {
            Id = id,
            Title = id,
            Content = new object(),
        };

    /// <summary>
    /// Mirrors the shape of the Inference views: a root with a horizontal proportional dock
    /// holding two tool docks, one of which nests inside a vertical proportional dock.
    /// </summary>
    private static (RootDock Root, Tool ToolA, Tool ToolB, Tool ToolC) CreateLiveLayout()
    {
        var toolA = CreateTool("ToolA");
        var toolB = CreateTool("ToolB");
        var toolC = CreateTool("ToolC");

        var root = new RootDock
        {
            Id = "Root",
            VisibleDockables =
            [
                new ProportionalDock
                {
                    Id = "MainLayout",
                    Orientation = Orientation.Horizontal,
                    VisibleDockables =
                    [
                        new ToolDock
                        {
                            Id = "PaneA",
                            Proportion = 0.3,
                            VisibleDockables = [toolA],
                        },
                        new ProportionalDockSplitter { Id = "Splitter1" },
                        new ProportionalDock
                        {
                            Id = "RightLayout",
                            Orientation = Orientation.Vertical,
                            VisibleDockables =
                            [
                                new ToolDock
                                {
                                    Id = "PaneB",
                                    Proportion = 0.8,
                                    VisibleDockables = [toolB],
                                },
                                new ToolDock
                                {
                                    Id = "PaneC",
                                    Proportion = 0.2,
                                    VisibleDockables = [toolC],
                                },
                            ],
                        },
                    ],
                },
            ],
        };

        return (root, toolA, toolB, toolC);
    }

    private static IEnumerable<IDockable> Flatten(IDockable dockable)
    {
        yield return dockable;

        if (dockable is not IDock { VisibleDockables: { } children })
            yield break;

        foreach (var child in children)
        foreach (var descendant in Flatten(child))
        {
            yield return descendant;
        }
    }

    private static IDock SerializeRoundTrip(IDock layout)
    {
        var json = Serializer.Serialize(layout);
        return Serializer.Deserialize<IDock?>(json)
            ?? throw new AssertFailedException("Deserialize returned null");
    }

    [TestMethod]
    public void TryGraftContent_RoundTrip_ReplacesToolsWithLiveInstances()
    {
        var (live, toolA, toolB, toolC) = CreateLiveLayout();

        var skeleton = SerializeRoundTrip(live);

        Assert.IsTrue(DockLayoutGrafter.TryGraftContent(skeleton, live));

        var grafted = Flatten(skeleton).OfType<Tool>().ToDictionary(t => t.Id!);
        Assert.AreEqual(3, grafted.Count);
        Assert.AreSame(toolA, grafted["ToolA"]);
        Assert.AreSame(toolB, grafted["ToolB"]);
        Assert.AreSame(toolC, grafted["ToolC"]);
        Assert.IsNotNull(grafted["ToolA"].Content);
    }

    [TestMethod]
    public void TryGraftContent_RoundTrip_KeepsSavedStructure()
    {
        var (live, _, _, _) = CreateLiveLayout();

        // User dragged: PaneA proportion changed
        var paneA = Flatten(live).OfType<ToolDock>().First(d => d.Id == "PaneA");
        paneA.Proportion = 0.55;

        var skeleton = SerializeRoundTrip(live);

        // Live layout "resets" (fresh view) before the saved layout is applied
        paneA.Proportion = 0.3;

        Assert.IsTrue(DockLayoutGrafter.TryGraftContent(skeleton, live));

        var restoredPaneA = Flatten(skeleton).OfType<ToolDock>().First(d => d.Id == "PaneA");
        Assert.AreEqual(0.55, restoredPaneA.Proportion, 0.001);
    }

    [TestMethod]
    public void TryGraftContent_RemapsActiveDockableToLiveInstance()
    {
        var (live, _, toolB, _) = CreateLiveLayout();

        var livePaneB = Flatten(live).OfType<ToolDock>().First(d => d.Id == "PaneB");
        livePaneB.ActiveDockable = toolB;

        var skeleton = SerializeRoundTrip(live);

        Assert.IsTrue(DockLayoutGrafter.TryGraftContent(skeleton, live));

        var skeletonPaneB = Flatten(skeleton).OfType<ToolDock>().First(d => d.Id == "PaneB");
        Assert.AreSame(toolB, skeletonPaneB.ActiveDockable);
    }

    [TestMethod]
    public void TryGraftContent_SavedLayoutMissingTool_IsRejected()
    {
        var (live, _, _, _) = CreateLiveLayout();
        var skeleton = SerializeRoundTrip(live);

        // Live layout gained a pane the saved layout doesn't know about (app update)
        var livePaneA = Flatten(live).OfType<ToolDock>().First(d => d.Id == "PaneA");
        livePaneA.VisibleDockables!.Add(CreateTool("ToolNew"));

        Assert.IsFalse(DockLayoutGrafter.TryGraftContent(skeleton, live));
    }

    [TestMethod]
    public void TryGraftContent_SavedLayoutWithExtraTool_IsRejected()
    {
        var (live, _, _, _) = CreateLiveLayout();
        var skeleton = SerializeRoundTrip(live);

        // Saved layout references a pane that no longer exists in the app
        var skeletonPaneA = Flatten(skeleton).OfType<ToolDock>().First(d => d.Id == "PaneA");
        skeletonPaneA.VisibleDockables!.Add(CreateTool("ToolRemoved"));

        Assert.IsFalse(DockLayoutGrafter.TryGraftContent(skeleton, live));
    }

    [TestMethod]
    public void TryGraftContent_DuplicateLiveToolIds_IsRejected()
    {
        var (live, _, _, _) = CreateLiveLayout();
        var skeleton = SerializeRoundTrip(live);

        var livePaneA = Flatten(live).OfType<ToolDock>().First(d => d.Id == "PaneA");
        livePaneA.VisibleDockables!.Add(CreateTool("ToolB"));

        Assert.IsFalse(DockLayoutGrafter.TryGraftContent(skeleton, live));
    }

    [TestMethod]
    public void TryGraftContent_MissingToolId_IsRejected()
    {
        var (live, _, _, _) = CreateLiveLayout();
        var skeleton = SerializeRoundTrip(live);

        var livePaneA = Flatten(live).OfType<ToolDock>().First(d => d.Id == "PaneA");
        livePaneA.VisibleDockables![0].Id = string.Empty;

        Assert.IsFalse(DockLayoutGrafter.TryGraftContent(skeleton, live));
    }
}
