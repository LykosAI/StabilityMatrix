using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace StabilityMatrix.Avalonia.Models.Inference;

public class ComponentDictionary<T> : Dictionary<string, T>, IJsonLoadableState where T : IJsonLoadableState
{
    /// <inheritdoc />
    public void LoadStateFromJsonObject(JsonObject state)
    {
        // For each existing key, load the state from the json object
        foreach (var (key, value) in state)
        {
            if (value is null) continue;
            
            if (TryGetValue(key, out var existingValue))
            {
                existingValue.LoadStateFromJsonObject(value.AsObject());
            }
        }
    }
    
    /// <inheritdoc />
    public JsonObject SaveStateToJsonObject()
    {
        // Create a new json object
        var state = new JsonObject();
        
        // For each existing key, save the state to the json object
        foreach (var (key, value) in this)
        {
            state.Add(key, value.SaveStateToJsonObject());
        }
        
        return state;
    }
}
