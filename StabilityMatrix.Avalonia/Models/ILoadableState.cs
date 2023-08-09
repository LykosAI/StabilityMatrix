using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StabilityMatrix.Avalonia.Models;

public interface ILoadableState<T> : IJsonLoadableState
{
    new Type LoadableStateType => typeof(T);
    
    void LoadState(T state);

    new void LoadStateFromJsonObject(JsonObject state)
    {
        state.Deserialize(LoadableStateType);
    }
    
    T SaveState();
    
    new JsonObject SaveStateToJsonObject()
    {
        var node = JsonSerializer.SerializeToNode(SaveState());
        return node?.AsObject() ?? throw new 
            InvalidOperationException("Failed to serialize state to JSON object.");
    }
}
