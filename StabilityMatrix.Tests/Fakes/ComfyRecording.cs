using System.Text.Json;
using System.Text.Json.Nodes;

namespace StabilityMatrix.Tests.Fakes;

/// <summary>
/// HTTP and websocket traffic recorded from a real ComfyUI server by <c>TestData/Comfy/record.py</c>.
/// </summary>
public sealed class ComfyRecording
{
    /// <summary>Version of ComfyUI the recording was made against, such as <c>0.38.0</c>.</summary>
    public required string ComfyUIVersion { get; init; }

    /// <summary>
    /// <c>/object_info/{node}</c> responses by node name. A node the server didn't have is an
    /// empty object, as ComfyUI returns it.
    /// </summary>
    public required IReadOnlyDictionary<string, JsonObject> ObjectInfo { get; init; }

    /// <summary>Recorded prompts by the name <c>record.py</c> gives them.</summary>
    public required IReadOnlyDictionary<string, ComfyRecordedPrompt> Prompts { get; init; }

    /// <summary>The status message ComfyUI sends when a websocket connects, carrying its <c>sid</c>.</summary>
    public required JsonObject ConnectStatus { get; init; }

    /// <summary>The client id the recording connected with, to be replaced by the connecting client's.</summary>
    public required string ClientId { get; init; }

    /// <summary>Bytes served at <c>/view</c> for the success prompt's first image.</summary>
    public required byte[] ViewImage { get; init; }

    /// <summary>Loads the recording in <c>TestData/Comfy/{version}</c> next to the test assembly.</summary>
    public static ComfyRecording Load(string version = "0.38.0")
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "TestData", "Comfy", version);
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "recording.json")))!.AsObject();

        var objectInfo = root["object_info"]!
            .AsObject()
            .ToDictionary(p => p.Key, p => p.Value!["body"]!.AsObject());

        var messages = root["websocket"]!
            .AsArray()
            .Select(m => m!["text"]?.AsObject())
            .OfType<JsonObject>()
            .ToList();

        // A prompt's messages run from the end of the previous prompt's to its own last message,
        // so the status messages sent when it was queued belong to it.
        var prompts = new Dictionary<string, ComfyRecordedPrompt>();
        var start = 1;
        foreach (var prompt in root["prompts"]!.AsArray().Select(p => p!.AsObject()))
        {
            var name = prompt["name"]!.GetValue<string>();
            var promptId = prompt["response"]!["prompt_id"]?.GetValue<string>();

            var own = new List<string>();
            if (promptId is not null)
            {
                var end = messages.FindLastIndex(m =>
                    m["data"]?["prompt_id"]?.GetValue<string>() == promptId
                );
                own.AddRange(messages[start..(end + 1)].Select(m => m.ToJsonString()));
                start = end + 1;
            }

            prompts[name] = new ComfyRecordedPrompt
            {
                Name = name,
                PromptId = promptId,
                Status = prompt["status"]!.GetValue<int>(),
                Response = prompt["response"]!.ToJsonString(),
                History = prompt["history"]?.ToJsonString(),
                Messages = own,
            };
        }

        return new ComfyRecording
        {
            ComfyUIVersion = root["comfyui_version"]!.GetValue<string>(),
            ClientId = root["client_id"]!.GetValue<string>(),
            ObjectInfo = objectInfo,
            Prompts = prompts,
            ConnectStatus = messages[0],
            ViewImage = File.ReadAllBytes(Path.Combine(directory, "view.png")),
        };
    }
}

/// <summary>One prompt as ComfyUI handled it: its <c>/prompt</c> response, websocket messages and history.</summary>
public sealed class ComfyRecordedPrompt
{
    /// <summary><c>success</c>, <c>runtime_error</c> or <c>validation_error</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The id ComfyUI gave the prompt; null when it was rejected.</summary>
    public required string? PromptId { get; init; }

    /// <summary>HTTP status of the <c>/prompt</c> response.</summary>
    public required int Status { get; init; }

    /// <summary>Body of the <c>/prompt</c> response.</summary>
    public required string Response { get; init; }

    /// <summary>Body of <c>/history/{prompt_id}</c> after the prompt finished.</summary>
    public required string? History { get; init; }

    /// <summary>Websocket text messages for the prompt, in the order ComfyUI sent them.</summary>
    public required IReadOnlyList<string> Messages { get; init; }

    /// <summary>
    /// <paramref name="json"/> with this prompt's recorded id replaced by <paramref name="promptId"/>.
    /// </summary>
    public string WithPromptId(string json, string promptId) =>
        PromptId is null ? json : json.Replace(PromptId, promptId, StringComparison.Ordinal);

    /// <summary>Parses a message's <c>type</c>.</summary>
    public static string TypeOf(string message)
    {
        using var document = JsonDocument.Parse(message);
        return document.RootElement.GetProperty("type").GetString()!;
    }
}
