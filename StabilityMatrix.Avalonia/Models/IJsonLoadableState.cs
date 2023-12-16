using System.Text.Json.Nodes;

namespace StabilityMatrix.Avalonia.Models;

public interface IJsonLoadableState
{
    void LoadStateFromJsonObject(JsonObject state, int version);

    void LoadStateFromJsonObject(JsonObject state);

    JsonObject SaveStateToJsonObject();
}
