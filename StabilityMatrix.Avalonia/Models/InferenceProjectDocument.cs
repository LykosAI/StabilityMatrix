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
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        IgnoreReadOnlyProperties = true,
        WriteIndented = true,
    };
    
    public int Version { get; set; } = 1;
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InferenceProjectType ProjectType { get; set; }
    
    public JsonObject? State { get; set; }

    public static InferenceProjectDocument FromLoadable(object loadableModel)
    {
        var document = new InferenceProjectDocument();

        if (loadableModel is InferenceTextToImageViewModel model)
        {
            document.ProjectType = InferenceProjectType.TextToImage;
            document.State = JsonSerializer.SerializeToNode(model.SaveState(), SerializerOptions)?.AsObject();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown loadable model type: {loadableModel.GetType()}"
            );
        }

        return document;
    }

    public Type GetViewModelType()
    {
        return ProjectType switch
        {
            InferenceProjectType.TextToImage => typeof(InferenceTextToImageViewModel),
            InferenceProjectType.Unknown => throw new InvalidOperationException(),
            _ => throw new ArgumentOutOfRangeException(nameof(ProjectType), ProjectType, null)
        };
    }
}
