using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StabilityMatrix.Avalonia.ViewModels.Inference;

namespace StabilityMatrix.Avalonia.Models;

/// <summary>
/// This is the project file for inference tabs
/// </summary>
[JsonSerializable(typeof(InferenceProjectDocument))]
public class InferenceProjectDocument
{
    [JsonIgnore]
    private static readonly JsonSerializerOptions SerializerOptions =
        new() { IgnoreReadOnlyProperties = true, WriteIndented = true, };

    public int Version { get; set; } = 2;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InferenceProjectType ProjectType { get; set; }

    public JsonObject? State { get; set; }

    public static InferenceProjectDocument FromLoadable(IJsonLoadableState loadableModel)
    {
        return new InferenceProjectDocument
        {
            ProjectType = loadableModel switch
            {
                InferenceTextToImageViewModel => InferenceProjectType.TextToImage,
                InferenceImageUpscaleViewModel => InferenceProjectType.Upscale,
                _
                    => throw new InvalidOperationException(
                        $"Unknown loadable model type: {loadableModel.GetType()}"
                    )
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
}
