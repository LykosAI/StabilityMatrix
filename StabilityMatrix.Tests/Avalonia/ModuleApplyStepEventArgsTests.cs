using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class ModuleApplyStepEventArgsTests
{
    private static ModelNodeConnection Model(string node) => new() { Data = [node, 0] };

    private static ModuleApplyStepEventArgs CreateArgs(bool withRefiner)
    {
        var builder = new ComfyNodeBuilder();
        builder.Connections.Base.Model = Model("base");

        if (withRefiner)
        {
            builder.Connections.Refiner.Model = Model("refiner");
        }

        var args = new ModuleApplyStepEventArgs { Builder = builder };
        args.Temp = args.CreateTempFromBuilder();
        return args;
    }

    /// <summary>
    /// The base sampler reads Temp.Base.Model, so a patch must never leave the refiner's model
    /// there (FreeU, NRS and ModelSamplingDiscrete used to).
    /// </summary>
    [TestMethod]
    public void PatchModels_WithRefiner_KeepsEachModelInItsOwnSlot()
    {
        var args = CreateArgs(withRefiner: true);

        args.PatchModels((name, model) => Model($"{model.Data![0]}_patched_{name}"));

        Assert.AreEqual("base_patched_Base", args.Temp.Base.Model!.Data![0]);
        Assert.AreEqual("refiner_patched_Refiner", args.Temp.Refiner.Model!.Data![0]);
        Assert.AreEqual("base_patched_Base", args.Builder.Connections.Base.Model!.Data![0]);
        Assert.AreEqual("refiner_patched_Refiner", args.Builder.Connections.Refiner.Model!.Data![0]);
    }

    [TestMethod]
    public void PatchModels_WithoutRefiner_PatchesOnlyBase()
    {
        var args = CreateArgs(withRefiner: false);
        var patched = new List<string>();

        args.PatchModels(
            (name, model) =>
            {
                patched.Add(name);
                return model;
            }
        );

        CollectionAssert.AreEqual(new[] { "Base" }, patched);
        Assert.IsNull(args.Builder.Connections.Refiner.Model);
    }
}
