using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference.Modules;
using StabilityMatrix.Avalonia.Views.Inference;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Inference;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(InferenceImageToImageView), IsPersistent = true)]
[RegisterTransient<InferenceImageToImageViewModel>, ManagedService]
public class InferenceImageToImageViewModel : InferenceTextToImageViewModel
{
    [JsonPropertyName("SelectImage")]
    public SelectImageCardViewModel SelectImageCardViewModel { get; }

    /// <inheritdoc />
    public InferenceImageToImageViewModel(
        ServiceManager<ViewModelBase> vmFactory,
        IInferenceClientManager inferenceClientManager,
        INotificationService notificationService,
        ISettingsManager settingsManager,
        IModelIndexService modelIndexService,
        RunningPackageService runningPackageService
    )
        : base(
            notificationService,
            inferenceClientManager,
            settingsManager,
            vmFactory,
            modelIndexService,
            runningPackageService
        )
    {
        SelectImageCardViewModel = vmFactory.Get<SelectImageCardViewModel>(vm =>
        {
            vm.IsMaskEditorEnabled = true;
        });

        SamplerCardViewModel.IsDenoiseStrengthEnabled = true;
    }

    /// <inheritdoc />
    protected override void BuildPrompt(BuildPromptEventArgs args)
    {
        var builder = args.Builder;

        // Setup constants
        builder.Connections.Seed = args.SeedOverride switch
        {
            { } seed => Convert.ToUInt64(seed),
            _ => Convert.ToUInt64(SeedCardViewModel.Seed)
        };

        var applyArgs = args.ToModuleApplyStepEventArgs();

        BatchSizeCardViewModel.ApplyStep(applyArgs);

        // Load models
        ModelCardViewModel.ApplyStep(applyArgs);

        // Setup image latent source
        SelectImageCardViewModel.ApplyStep(applyArgs);

        // Prompts and loras
        PromptCardViewModel.ApplyStep(applyArgs);

        // Setup Sampler and Refiner if enabled
        var isUnetLoader = ModelCardViewModel.SelectedModelLoader is ModelLoader.Gguf or ModelLoader.Unet;
        if (isUnetLoader)
        {
            SamplerCardViewModel.ApplyStepsInitialFluxSampler(applyArgs);
        }
        else
        {
            SamplerCardViewModel.ApplyStep(applyArgs);
        }

        // Apply module steps
        foreach (var module in ModulesCardViewModel.Cards.OfType<ModuleBase>())
        {
            module.ApplyStep(applyArgs);
        }

        applyArgs.InvokeAllPreOutputActions();

        builder.SetupOutputImage();
    }

    /// <inheritdoc />
    protected override IEnumerable<ImageSource> GetInputImages()
    {
        var mainImages = SelectImageCardViewModel.GetInputImages();

        var samplerImages = SamplerCardViewModel
            .ModulesCardViewModel.Cards.OfType<IInputImageProvider>()
            .SelectMany(m => m.GetInputImages());

        var moduleImages = ModulesCardViewModel
            .Cards.OfType<IInputImageProvider>()
            .SelectMany(m => m.GetInputImages());

        return mainImages.Concat(samplerImages).Concat(moduleImages);
    }
}
