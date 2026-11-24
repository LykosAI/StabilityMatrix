using System.Collections.Specialized;
using DynamicData.Binding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Tests.Fakes;

namespace StabilityMatrix.Tests.Avalonia;

/// <summary>
/// <see cref="InferenceClientManager"/> connected to <see cref="FakeComfyServer"/>, filling its lists
/// from <c>/object_info</c> responses recorded from a real ComfyUI.
/// </summary>
[TestClass]
public class InferenceClientManagerFakeServerTests
{
    private FakeComfyServer server = null!;
    private IModelIndexService modelIndex = null!;
    private TestClientManager manager = null!;

    [TestInitialize]
    public async Task TestInitialize()
    {
        SynchronizationContext.SetSynchronizationContext(new InlineSynchronizationContext());

        server = await FakeComfyServer.StartAsync();

        modelIndex = Substitute.For<IModelIndexService>();
        modelIndex.FindByModelType(Arg.Any<SharedFolderType>()).Returns([]);

        var httpClientFactory = new ServiceCollection()
            .AddHttpClient()
            .BuildServiceProvider()
            .GetRequiredService<IHttpClientFactory>();

        manager = new TestClientManager(
            NullLogger<InferenceClientManager>.Instance,
            new ApiFactory(httpClientFactory),
            modelIndex,
            Substitute.For<ISettingsManager>(),
            Substitute.For<ICompletionProvider>()
        );

        await manager.ConnectAsyncImpl(server.BaseAddress);
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        await manager.CloseAsync();
        manager.Dispose();
        await server.DisposeAsync();
    }

    [TestMethod]
    public void Connect_FillsListsFromServer()
    {
        CollectionAssert.IsSubsetOf(
            new[] { "broken_checkpoint.safetensors", "sd_xl_base_1.0.safetensors" },
            Names(manager.Models)
        );
        CollectionAssert.IsSubsetOf(
            new[] { "detail_tweaker_xl.safetensors", @"style\ink_sketch.safetensors" },
            Names(manager.LoraModels)
        );
        CollectionAssert.Contains(Names(manager.UnetModels), "flux1-dev.safetensors");
        CollectionAssert.IsSubsetOf(
            new[] { "clip_l.safetensors", "t5xxl_fp16.safetensors" },
            Names(manager.ClipModels)
        );
        CollectionAssert.Contains(Names(manager.ControlNetModels), "control_canny_sdxl.safetensors");
        CollectionAssert.Contains(Names(manager.ClipVisionModels), "clip_vision_h.safetensors");
        CollectionAssert.Contains(manager.Upscalers.Select(u => u.Name).ToList(), "4x-UltraSharp.pth");

        CollectionAssert.IsSubsetOf(
            new[] { "euler", "dpmpp_2m" },
            manager.Samplers.Select(s => s.Name).ToList()
        );
        CollectionAssert.IsSubsetOf(
            new[] { "karras", "simple" },
            manager.Schedulers.Select(s => s.Name).ToList()
        );
    }

    /// <summary>
    /// Models only the server has are never removed while the lists reload after a local index
    /// change. Removing one, even for a moment, clears a dropdown selected on it.
    /// </summary>
    [TestMethod]
    public async Task IndexChange_NeverRemovesServerOnlyModels()
    {
        var lists = new Dictionary<string, IObservableCollection<HybridModelFile>>
        {
            [nameof(manager.Models)] = manager.Models,
            [nameof(manager.LoraModels)] = manager.LoraModels,
            [nameof(manager.UnetModels)] = manager.UnetModels,
            [nameof(manager.ClipModels)] = manager.ClipModels,
            [nameof(manager.ControlNetModels)] = manager.ControlNetModels,
            [nameof(manager.ClipVisionModels)] = manager.ClipVisionModels,
        };
        var removed = new List<string>();
        foreach (var (name, list) in lists)
        {
            list.CollectionChanged += (_, e) =>
            {
                if (e.Action is NotifyCollectionChangedAction.Reset)
                    removed.Add($"{name}: reset");
                foreach (var item in e.OldItems?.OfType<HybridModelFile>() ?? [])
                    removed.Add($"{name}: {item.RelativePath}");
            };
        }

        await manager.ReloadAfterIndexChangeAsync();

        CollectionAssert.AreEqual(Array.Empty<string>(), removed, string.Join(", ", removed));
    }

    [TestMethod]
    public async Task IndexChange_PicksUpModelsTheServerAdded()
    {
        var input = server.ObjectInfo["CheckpointLoaderSimple"]["CheckpointLoaderSimple"]!["input"]!;
        input["required"]!["ckpt_name"]![0]!.AsArray().Add("new_checkpoint.safetensors");

        await manager.ReloadAfterIndexChangeAsync();

        CollectionAssert.Contains(Names(manager.Models), "new_checkpoint.safetensors");
    }

    private static List<string> Names(IObservableCollection<HybridModelFile> models) =>
        models.Select(m => m.RelativePath).ToList();

    private sealed class TestClientManager(
        Microsoft.Extensions.Logging.ILogger<InferenceClientManager> logger,
        IApiFactory apiFactory,
        IModelIndexService modelIndexService,
        ISettingsManager settingsManager,
        ICompletionProvider completionProvider
    ) : InferenceClientManager(logger, apiFactory, modelIndexService, settingsManager, completionProvider)
    {
        /// <summary>What the model index changed handler runs on the UI thread, awaited.</summary>
        public async Task ReloadAfterIndexChangeAsync()
        {
            ResetSharedProperties();
            await LoadSharedPropertiesAsync();
        }
    }

    private sealed class InlineSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => d(state);
    }
}
