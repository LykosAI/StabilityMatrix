using DynamicData;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockInferenceClientManager : InferenceClientManager
{
    public MockInferenceClientManager(
        ILogger<InferenceClientManager> logger,
        IApiFactory apiFactory,
        IModelIndexService modelIndexService,
        ISettingsManager settingsManager,
        ICompletionProvider completionProvider
    )
        : base(logger, apiFactory, modelIndexService, settingsManager, completionProvider)
    {
        // Load our initial models
        ResetSharedProperties();
    }

    public new bool IsConnected { get; set; }

    protected override Task LoadSharedPropertiesAsync()
    {
        if (Models.Any(m => m.IsRemote))
        {
            return Task.CompletedTask;
        }

        Models.Add(
            [
                HybridModelFile.FromRemote("v1-5-pruned-emaonly.safetensors"),
                HybridModelFile.FromRemote("art-shaper1.safetensors"),
            ]
        );

        return Task.CompletedTask;
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnecting = true;
        await Task.Delay(5000, cancellationToken);

        IsConnecting = false;
        IsConnected = true;
    }

    public override async Task ConnectAsync(
        PackagePair packagePair,
        CancellationToken cancellationToken = default
    )
    {
        await ConnectAsync(cancellationToken);
    }
}
