using System;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Models.Base;

namespace StabilityMatrix.Avalonia.Models.Inference;

public record EditableModule : StringValue
{
    public static readonly EditableModule FreeU =
        new(
            "FreeU",
            builder =>
                builder.Get<StackExpanderViewModel>(vm =>
                {
                    vm.Title = "FreeU";
                    vm.AddCards(builder.Get<FreeUCardViewModel>());
                })
        );

    public static readonly EditableModule HiresFix =
        new(
            "HiresFix",
            builder =>
                builder.Get<StackExpanderViewModel>(vm =>
                {
                    vm.Title = "HiresFix";
                    vm.AddCards(
                        builder.Get<UpscalerCardViewModel>(),
                        builder.Get<SamplerCardViewModel>(vmSampler =>
                        {
                            vmSampler.IsDenoiseStrengthEnabled = true;
                        })
                    );
                })
        );

    public static readonly EditableModule Upscaler =
        new(
            "Upscaler",
            builder =>
                builder.Get<StackExpanderViewModel>(vm =>
                {
                    vm.Title = "Upscaler";
                    vm.AddCards(builder.Get<UpscalerCardViewModel>());
                })
        );

    public Func<IServiceManager<ViewModelBase>, ViewModelBase> Build { get; }

    private EditableModule(string value, Func<IServiceManager<ViewModelBase>, ViewModelBase> build)
        : base(value)
    {
        Build = build;
    }
}
