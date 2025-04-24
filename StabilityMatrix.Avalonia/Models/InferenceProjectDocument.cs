using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Models.Inference;

namespace StabilityMatrix.Avalonia.Models;

/// <summary>
/// This is the project file for inference tabs
/// </summary>
public class InferenceProjectDocument : ICloneable
{
    [JsonIgnore]
    private static readonly JsonSerializerOptions SerializerOptions =
        new() { IgnoreReadOnlyProperties = true, WriteIndented = true, };

    public int Version { get; set; } = 2;

    [JsonConverter(typeof(JsonStringEnumConverter<InferenceProjectType>))]
    public InferenceProjectType ProjectType { get; set; }

    public JsonObject? State { get; set; }

    public static InferenceProjectDocument FromLoadable(IJsonLoadableState loadableModel)
    {
        return new InferenceProjectDocument
        {
            ProjectType = loadableModel switch
            {
                InferenceImageToImageViewModel => InferenceProjectType.ImageToImage,
                InferenceTextToImageViewModel => InferenceProjectType.TextToImage,
                InferenceImageUpscaleViewModel => InferenceProjectType.Upscale,
                InferenceImageToVideoViewModel => InferenceProjectType.ImageToVideo,
                InferenceFluxTextToImageViewModel => InferenceProjectType.FluxTextToImage,
                InferenceWanImageToVideoViewModel => InferenceProjectType.WanImageToVideo,
                InferenceWanTextToVideoViewModel => InferenceProjectType.WanTextToVideo
            },
            State = loadableModel.SaveStateToJsonObject()
        };
    }

    public void VerifyVersion()
    {
        if (Version < 2)
        {
            throw new NotSupportedException(
                $"Project was created in an earlier pre-release version of Stability Matrix and is no longer supported. "
                    + $"Please create a new project."
            );
        }
    }

    public SeedCardModel? GetSeedModel()
    {
        if (State is null || !State.TryGetPropertyValue("Seed", out var seedCard))
        {
            return null;
        }

        return seedCard.Deserialize<SeedCardModel>();
    }

    /// <summary>
    /// Returns a new <see cref="InferenceProjectDocument"/> with the State modified.
    /// </summary>
    /// <param name="stateModifier">Action that changes the state</param>
    public InferenceProjectDocument WithState(Action<JsonObject?> stateModifier)
    {
        var document = (InferenceProjectDocument)Clone();
        stateModifier(document.State);
        return document;
    }

    public bool TryUpdateModel<T>(string key, Func<T, T> modifier)
    {
        if (State is not { } state)
            return false;

        if (!state.TryGetPropertyValue(key, out var modelNode))
        {
            return false;
        }

        if (modelNode.Deserialize<T>() is not { } model)
        {
            return false;
        }

        modelNode = JsonSerializer.SerializeToNode(modifier(model));

        state[key] = modelNode;

        return true;
    }

    public bool TryUpdateModel(string key, Func<JsonNode, JsonNode> modifier)
    {
        if (State is not { } state)
            return false;

        if (!state.TryGetPropertyValue(key, out var modelNode) || modelNode is null)
        {
            return false;
        }

        state[key] = modifier(modelNode);

        return true;
    }

    public InferenceProjectDocument WithBatchSize(int batchSize, int batchCount)
    {
        if (State is null)
            throw new InvalidOperationException("State is null");

        var document = (InferenceProjectDocument)Clone();

        var batchSizeCard =
            document.State!["BatchSize"] ?? throw new InvalidOperationException("BatchSize card is null");

        batchSizeCard["BatchSize"] = batchSize;
        batchSizeCard["BatchCount"] = batchCount;

        return document;
    }

    /// <inheritdoc />
    public object Clone()
    {
        var newObj = (InferenceProjectDocument)MemberwiseClone();
        // Clone State also since its mutable
        newObj.State = State == null ? null : JsonSerializer.SerializeToNode(State).Deserialize<JsonObject>();
        return newObj;
    }
}
