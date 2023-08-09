using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using StabilityMatrix.Avalonia.Models;

namespace StabilityMatrix.Avalonia.ViewModels;

public abstract class LoadableViewModelBase : ViewModelBase, IJsonLoadableState
{
    /// <inheritdoc />
    public abstract void LoadStateFromJsonObject(JsonObject state);

    /// <inheritdoc />
    public abstract JsonObject SaveStateToJsonObject();
    
    /// <summary>
    /// Serialize a model to a JSON object.
    /// </summary>
    protected static JsonObject SerializeModel<T>(T model)
    {
        var node = JsonSerializer.SerializeToNode(model);
        return node?.AsObject() ?? throw new 
            NullReferenceException("Failed to serialize state to JSON object.");
    }
    
    /// <summary>
    /// Deserialize a model from a JSON object.
    /// </summary>
    protected static T DeserializeModel<T>(JsonObject state)
    {
        return state.Deserialize<T>() ?? throw new 
            NullReferenceException("Failed to deserialize state from JSON object.");
    }
}
