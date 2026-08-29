using System.Text.Json.Nodes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Packages.Extensions;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class WorkflowNodeAnalyzerTests
{
    private static JsonObject Workflow(params string[] nodeJsons) =>
        (JsonObject)JsonNode.Parse($$"""{"nodes": [{{string.Join(",", nodeJsons)}}]}""")!;

    [TestMethod]
    public void GroupsNodesByPack_SkippingComfyCore()
    {
        var workflow = Workflow(
            """{"type": "LoadImage", "properties": {"cnr_id": "comfy-core"}}""",
            """{"type": "KJNodesA", "properties": {"cnr_id": "comfyui-kjnodes"}}""",
            """{"type": "KJNodesB", "properties": {"cnr_id": "comfyui-kjnodes", "aux_id": "ComfyUI-KJNodes"}}""",
            """{"type": "UmeNode", "properties": {"aux_id": "ComfyUI-UmeAiRT-Toolkit"}}"""
        );

        var packs = WorkflowNodeAnalyzer.GetRequiredPacks(workflow);

        Assert.AreEqual(2, packs.Count);

        var kjNodes = packs.Single(p => p.CnrId == "comfyui-kjnodes");
        Assert.AreEqual(2, kjNodes.NodeTypes.Count);

        var ume = packs.Single(p => p.AuxId == "ComfyUI-UmeAiRT-Toolkit");
        Assert.AreEqual("ComfyUI-UmeAiRT-Toolkit", ume.DisplayName);
    }

    [TestMethod]
    public void LegacyWorkflowWithoutIds_YieldsSingleUnidentifiedPack()
    {
        var workflow = Workflow(
            """{"type": "SomeCustomNode", "properties": {}}""",
            """{"type": "AnotherNode"}"""
        );

        var packs = WorkflowNodeAnalyzer.GetRequiredPacks(workflow);

        Assert.AreEqual(1, packs.Count);
        Assert.IsTrue(packs[0].IsUnidentified);
        Assert.AreEqual(2, packs[0].NodeTypes.Count);
    }

    [TestMethod]
    public void ModernWorkflow_DoesNotReportUnidentifiedNoise()
    {
        // A modern file where one node lacks ids should not produce an extra
        // "unknown" entry next to the real pack entries
        var workflow = Workflow(
            """{"type": "KJNode", "properties": {"cnr_id": "comfyui-kjnodes"}}""",
            """{"type": "Reroute", "properties": {}}"""
        );

        var packs = WorkflowNodeAnalyzer.GetRequiredPacks(workflow);

        Assert.AreEqual(1, packs.Count);
        Assert.AreEqual("comfyui-kjnodes", packs[0].CnrId);
    }

    [TestMethod]
    public void MatchExtension_PrefersRegistryId_ThenRepoName()
    {
        var byId = MakeExtension(id: "comfyui-kjnodes", repo: "SomethingElse", title: "Other");
        var byRepo = MakeExtension(id: null, repo: "ComfyUI-UmeAiRT-Toolkit", title: "Ume Toolkit");
        var extensions = new[] { byId, byRepo };

        var idPack = new WorkflowNodePack { CnrId = "comfyui-kjnodes" };
        Assert.AreSame(byId, WorkflowNodeAnalyzer.MatchExtension(idPack, extensions));

        var auxPack = new WorkflowNodePack { AuxId = "UmeAiRT/ComfyUI-UmeAiRT-Toolkit" };
        Assert.AreSame(byRepo, WorkflowNodeAnalyzer.MatchExtension(auxPack, extensions));

        var missingPack = new WorkflowNodePack { CnrId = "not-in-index" };
        Assert.IsNull(WorkflowNodeAnalyzer.MatchExtension(missingPack, extensions));
    }

    private static PackageExtension MakeExtension(string? id, string repo, string title) =>
        new()
        {
            Id = id,
            Author = "author",
            Title = title,
            Reference = new Uri($"https://github.com/author/{repo}"),
            Files = [new Uri($"https://github.com/author/{repo}")],
        };
}
