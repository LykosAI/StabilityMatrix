using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference.Modules;
using StabilityMatrix.Avalonia.Views.Inference;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(InferenceImageToImageView), IsPersistent = true)]
[Transient, ManagedService]
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
        SelectImageCardViewModel = vmFactory.Get<SelectImageCardViewModel>();

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

        BatchSizeCardViewModel.ApplyStep(args);

        // Load models
        ModelCardViewModel.ApplyStep(args);

        // Setup image latent source
        SelectImageCardViewModel.ApplyStep(args);

        // Prompts and loras
        PromptCardViewModel.ApplyStep(args);

        // Setup Sampler and Refiner if enabled
        SamplerCardViewModel.ApplyStep(args);

        // Apply module steps
        foreach (var module in ModulesCardViewModel.Cards.OfType<ModuleBase>())
        {
            module.ApplyStep(args);
        }

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
