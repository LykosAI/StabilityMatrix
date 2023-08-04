using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StabilityMatrix.Avalonia.Models;

public interface ILoadableState<T>
{
    public Type LoadableStateType => typeof(T);
    
    public void LoadState(T state);

    public void LoadStateFromJsonObject(JsonObject state)
    {
        state.Deserialize(LoadableStateType);
    }
    
    public T SaveState();
}
