using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class InferenceClientManagerResetTests
{
    private IModelIndexService modelIndex = null!;
    private TestClientManager manager = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        SynchronizationContext.SetSynchronizationContext(new InlineSynchronizationContext());

        modelIndex = Substitute.For<IModelIndexService>();
        modelIndex.FindByModelType(Arg.Any<SharedFolderType>()).Returns([]);

        manager = new TestClientManager(
            NullLogger<InferenceClientManager>.Instance,
            Substitute.For<IApiFactory>(),
            modelIndex,
            Substitute.For<ISettingsManager>(),
            Substitute.For<ICompletionProvider>()
        );
    }

    [TestMethod]
    [DataRow(SharedFolderType.StableDiffusion)]
    [DataRow(SharedFolderType.DiffusionModels)]
    [DataRow(SharedFolderType.TextEncoders)]
    [DataRow(SharedFolderType.Lora)]
    public void IndexRefresh_WhileConnected_KeepsEntriesTheServerFills(SharedFolderType folder)
    {
        SetIndex(folder, "a.safetensors");
        manager.Reset();

        manager.Connected = true;
        SetIndex(folder, "b.safetensors");
        manager.Reset();

        CollectionAssert.AreEquivalent(new[] { "a.safetensors", "b.safetensors" }, Names(folder));
    }

    [TestMethod]
    public void IndexRefresh_WhileDisconnected_MatchesTheIndex()
    {
        SetIndex(SharedFolderType.DiffusionModels, "a.safetensors");
        manager.Reset();

        SetIndex(SharedFolderType.DiffusionModels, "b.safetensors");
        manager.Reset();

        CollectionAssert.AreEquivalent(new[] { "b.safetensors" }, Names(SharedFolderType.DiffusionModels));
    }

    [TestMethod]
    public void IndexRefresh_WhileConnected_StillResetsLocalOnlyLists()
    {
        // VAEs are not loaded from the server, so the local index stays their only source.
        SetIndex(SharedFolderType.VAE, "a.safetensors");
        manager.Reset();

        manager.Connected = true;
        SetIndex(SharedFolderType.VAE, "b.safetensors");
        manager.Reset();

        CollectionAssert.AreEquivalent(new[] { "b.safetensors" }, Names(SharedFolderType.VAE));
    }

    private void SetIndex(SharedFolderType folder, params string[] fileNames)
    {
        modelIndex
            .FindByModelType(Arg.Is<SharedFolderType>(t => t.HasFlag(folder)))
            .Returns(
                fileNames.Select(name => new LocalModelFile
                {
                    RelativePath = $"{folder}/{name}",
                    SharedFolderType = folder,
                })
            );
    }

    private string[] Names(SharedFolderType folder)
    {
        var collection = folder switch
        {
            SharedFolderType.StableDiffusion => manager.Models,
            SharedFolderType.DiffusionModels => manager.UnetModels,
            SharedFolderType.TextEncoders => manager.ClipModels,
            SharedFolderType.Lora => manager.LoraModels,
            SharedFolderType.VAE => manager.VaeModels,
            _ => throw new ArgumentOutOfRangeException(nameof(folder)),
        };

        // Some lists also show downloadable and built-in entries; only the indexed files matter here.
        return collection.Where(m => m.Type == HybridModelType.Local).Select(m => m.FileName).ToArray();
    }

    private sealed class TestClientManager(
        Microsoft.Extensions.Logging.ILogger<InferenceClientManager> logger,
        IApiFactory apiFactory,
        IModelIndexService modelIndexService,
        ISettingsManager settingsManager,
        ICompletionProvider completionProvider
    ) : InferenceClientManager(logger, apiFactory, modelIndexService, settingsManager, completionProvider)
    {
        public bool Connected { get; set; }

        public override bool IsConnected => Connected;

        public void Reset() => ResetSharedProperties();
    }

    private sealed class InlineSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => d(state);
    }
}
