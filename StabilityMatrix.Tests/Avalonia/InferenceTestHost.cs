using System.Reflection;
using System.Runtime.ExceptionServices;
using DynamicData.Binding;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StabilityMatrix.Avalonia;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;

namespace StabilityMatrix.Tests.Avalonia;

/// <summary>
/// Builds real Inference tab view models from <see cref="App.ConfigureServices"/>, over a substituted
/// <see cref="IInferenceClientManager"/> whose lists hold <see cref="ModelCatalog"/>.
/// </summary>
internal sealed class InferenceTestHost : IDisposable
{
    /// <summary>The local models every list on <see cref="ClientManager"/> starts with.</summary>
    public static IReadOnlyList<(SharedFolderType Folder, string File)> ModelCatalog { get; } =
    [
        (SharedFolderType.StableDiffusion, "sd_xl_base_1.0.safetensors"),
        (SharedFolderType.StableDiffusion, "svd_xt_1_1.safetensors"),
        (SharedFolderType.DiffusionModels, "flux1-dev.safetensors"),
        (SharedFolderType.DiffusionModels, "custom_unet_v3.safetensors"),
        (SharedFolderType.DiffusionModels, "wan2.1_t2v_1.3B_fp16.safetensors"),
        (SharedFolderType.DiffusionModels, "wan2.1_i2v_480p_14B_fp16.safetensors"),
        (SharedFolderType.TextEncoders, "clip_l.safetensors"),
        (SharedFolderType.TextEncoders, "t5xxl_fp16.safetensors"),
        (SharedFolderType.TextEncoders, "mistral_3_small_flux2_fp8.safetensors"),
        (SharedFolderType.TextEncoders, "umt5_xxl_fp8_e4m3fn_scaled.safetensors"),
        (SharedFolderType.VAE, "sdxl_vae.safetensors"),
        (SharedFolderType.VAE, "ae.safetensors"),
        (SharedFolderType.VAE, "flux2-vae.safetensors"),
        (SharedFolderType.VAE, "wan_2.1_vae.safetensors"),
        (SharedFolderType.Lora, "detail_tweaker_xl.safetensors"),
        (SharedFolderType.ClipVision, "clip_vision_h.safetensors"),
    ];

    // 1x1 transparent PNG
    private const string InputImagePng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    private readonly ServiceProvider services;
    private readonly List<IServiceScope> scopes = [];

    public InferenceTestHost()
    {
        var collection = App.ConfigureServices(disableMessagePipeInterprocess: true);

        ClientManager = Substitute.For<IInferenceClientManager>();
        foreach (var property in typeof(IInferenceClientManager).GetProperties())
        {
            if (
                property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(IObservableCollection<>)
            )
            {
                var itemType = property.PropertyType.GetGenericArguments()[0];
                var list = Activator.CreateInstance(
                    typeof(ObservableCollectionExtended<>).MakeGenericType(itemType)
                );
                property.GetValue(ClientManager).Returns(list);
            }
        }
        ClientManager.Samplers.Load(ComfySampler.Defaults);
        ClientManager.Schedulers.Load(ComfyScheduler.Defaults);
        ClientManager.Upscalers.Load(ComfyUpscaler.Defaults);
        ClientManager.Upscalers.Add(new ComfyUpscaler("4x-UltraSharp.pth", ComfyUpscalerType.ESRGAN));
        foreach (var (folder, file) in ModelCatalog)
        {
            ListFor(folder).Add(LocalModel(folder, file));
        }
        collection.AddSingleton(ClientManager);

        // Prompts look up <lora:name> tags in the model index
        var modelIndex = Substitute.For<IModelIndexService>();
        modelIndex.ModelIndex.Returns(
            ModelCatalog
                .GroupBy(m => m.Folder)
                .ToDictionary(g => g.Key, g => g.Select(m => LocalModel(m.Folder, m.File).Local!).ToList())
        );
        modelIndex.FindByModelType(Arg.Any<SharedFolderType>()).Returns([]);
        collection.AddSingleton(modelIndex);

        var settingsManager = Substitute.For<ISettingsManager>();
        settingsManager.Settings.Returns(new Settings());
        collection.AddSingleton(settingsManager);

        // The real provider needs Avalonia's asset loader
        var promptSyntaxFile = Assembly
            .GetExecutingAssembly()
            .GetManifestResourceStream("StabilityMatrix.Tests.ImagePrompt.tmLanguage.json")!;
        var grammar = new Registry(new RegistryOptions(ThemeName.DarkPlus)).LoadGrammarFromStream(
            promptSyntaxFile
        );
        var tokenizerProvider = Substitute.For<ITokenizerProvider>();
        tokenizerProvider.TokenizeLine(Arg.Any<string>()).Returns(x => grammar.TokenizeLine(x.Arg<string>()));
        collection.AddSingleton(tokenizerProvider);

        services = collection.BuildServiceProvider();

        InputImagePath = Path.Combine(Path.GetTempPath(), $"sm-inference-test-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(InputImagePath, Convert.FromBase64String(InputImagePng));
    }

    public IInferenceClientManager ClientManager { get; }

    /// <summary>A 1x1 PNG for tabs that take an input image.</summary>
    public string InputImagePath { get; }

    /// <summary>A view model of <paramref name="type"/> from a new scope, like a newly opened tab.</summary>
    public ViewModelBase Create(Type type)
    {
        var scope = services.CreateScope();
        scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IServiceManager<ViewModelBase>>().Get(type);
    }

    public T Create<T>()
        where T : ViewModelBase => (T)Create(typeof(T));

    /// <summary>The catalog model named <paramref name="file"/>, as listed on <see cref="ClientManager"/>.</summary>
    public HybridModelFile Model(string file) =>
        ModelCatalog
            .Where(m => m.File == file)
            .Select(m => ListFor(m.Folder).Single(x => x.FileName == file))
            .Single();

    public static HybridModelFile LocalModel(SharedFolderType folder, string file) =>
        HybridModelFile.FromLocal(
            new LocalModelFile
            {
                RelativePath = $"{folder.GetStringValue()}/{file}",
                SharedFolderType = folder,
            }
        );

    private IObservableCollection<HybridModelFile> ListFor(SharedFolderType folder) =>
        folder switch
        {
            SharedFolderType.StableDiffusion => ClientManager.Models,
            SharedFolderType.DiffusionModels => ClientManager.UnetModels,
            SharedFolderType.TextEncoders => ClientManager.ClipModels,
            SharedFolderType.VAE => ClientManager.VaeModels,
            SharedFolderType.Lora => ClientManager.LoraModels,
            SharedFolderType.ClipVision => ClientManager.ClipVisionModels,
            _ => throw new ArgumentOutOfRangeException(nameof(folder), folder, null),
        };

    /// <summary>The ComfyUI prompt <paramref name="vm"/> would queue.</summary>
    public static NodeDictionary BuildNodes(InferenceGenerationViewModelBase vm)
    {
        var args = new InferenceGenerationViewModelBase.BuildPromptEventArgs();
        try
        {
            vm.GetType()
                .GetMethod("BuildPrompt", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(vm, [args]);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
        }
        return args.Builder.ToNodeDictionary();
    }

    public void Dispose()
    {
        foreach (var scope in scopes)
        {
            scope.Dispose();
        }
        services.Dispose();
        File.Delete(InputImagePath);
    }
}
