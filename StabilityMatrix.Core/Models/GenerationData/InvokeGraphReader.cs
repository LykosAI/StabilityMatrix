using System.Text.Json.Nodes;

namespace StabilityMatrix.Core.Models.GenerationData;

/// <summary>
/// Reads the prompts and settings of an InvokeAI invocation graph ("invokeai_graph").
/// Values may be set on a node or come from another node through an edge.
/// </summary>
internal class InvokeGraphReader
{
    private const int MaxDepth = 16;

    private static readonly HashSet<string> NonPromptInputs =
    [
        "clip",
        "clip2",
        "t5_encoder",
        "mask",
        "unet",
        "vae",
    ];

    private readonly Dictionary<string, JsonObject> nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Node, string Field), List<(string Node, string Field)>> incoming = [];

    private InvokeGraphReader(JsonObject graphNodes, JsonArray edges)
    {
        foreach (var (id, node) in graphNodes)
        {
            if (node is JsonObject obj)
                nodes[id] = obj;
        }

        foreach (var edge in edges)
        {
            var source = GenerationDataJson.Get(edge, "source");
            var destination = GenerationDataJson.Get(edge, "destination");
            if (
                GenerationDataJson.GetString(GenerationDataJson.Get(source, "node_id")) is not { } sourceNode
                || GenerationDataJson.GetString(GenerationDataJson.Get(source, "field"))
                    is not { } sourceField
                || GenerationDataJson.GetString(GenerationDataJson.Get(destination, "node_id"))
                    is not { } destNode
                || GenerationDataJson.GetString(GenerationDataJson.Get(destination, "field"))
                    is not { } destField
            )
                continue;

            var key = (destNode, destField);
            if (!incoming.TryGetValue(key, out var sources))
                incoming[key] = sources = [];
            sources.Add((sourceNode, sourceField));
        }
    }

    public static GenerationParameters? Read(JsonObject? graph)
    {
        if (graph?["nodes"] is not JsonObject graphNodes || graph["edges"] is not JsonArray edges)
            return null;

        var reader = new InvokeGraphReader(graphNodes, edges);
        return reader.ReadCoreMetadata() ?? reader.ReadDenoiseNode();
    }

    private GenerationParameters? ReadCoreMetadata()
    {
        if (nodes.FirstOrDefault(n => Type(n.Value) == "core_metadata") is not ({ } id, { } node))
            return null;

        var parameters = new GenerationParameters
        {
            PositivePrompt = Trim(GenerationDataJson.GetString(Resolve(id, "positive_prompt"))),
            NegativePrompt = Trim(GenerationDataJson.GetString(Resolve(id, "negative_prompt"))),
            Seed = GenerationDataJson.GetSeed(Resolve(id, "seed")),
            Steps = (int)(GenerationDataJson.GetDouble(Resolve(id, "steps")) ?? 0),
            CfgScale = GenerationDataJson.GetDouble(Resolve(id, "cfg_scale") ?? Resolve(id, "guidance")) ?? 0,
            Sampler = GenerationDataJson.GetString(Resolve(id, "scheduler")),
            Width = (int)(GenerationDataJson.GetDouble(Resolve(id, "width")) ?? 0),
            Height = (int)(GenerationDataJson.GetDouble(Resolve(id, "height")) ?? 0),
            ModelName = ModelName(Resolve(id, "model")) ?? FindLoaderModel(),
        };

        // Upscale and canvas graphs leave the prompt out of the metadata node
        if (parameters.PositivePrompt is null && ReadDenoiseNode() is { } denoise)
        {
            parameters.PositivePrompt = denoise.PositivePrompt;
            parameters.NegativePrompt ??= denoise.NegativePrompt;
        }

        return parameters.PositivePrompt is null && parameters.Steps == 0 ? null : parameters;
    }

    private GenerationParameters? ReadDenoiseNode()
    {
        var denoise = nodes.FirstOrDefault(n =>
            n.Value.ContainsKey("steps") && n.Value.Any(p => p.Key.StartsWith("positive_"))
        );
        if (denoise.Value is null)
            return null;

        var id = denoise.Key;
        var positiveField = denoise.Value.ContainsKey("positive_conditioning")
            ? "positive_conditioning"
            : denoise.Value.First(p => p.Key.StartsWith("positive_")).Key;
        var negativeField = positiveField.Replace("positive_", "negative_");

        var parameters = new GenerationParameters
        {
            PositivePrompt = FindPrompt(id, positiveField),
            NegativePrompt = FindPrompt(id, negativeField),
            Steps = (int)(GenerationDataJson.GetDouble(Resolve(id, "steps")) ?? 0),
            CfgScale = GenerationDataJson.GetDouble(Resolve(id, "cfg_scale") ?? Resolve(id, "guidance")) ?? 0,
            Sampler = GenerationDataJson.GetString(Resolve(id, "scheduler")),
            ModelName = FindLoaderModel(),
        };

        foreach (var (noiseNode, _) in incoming.GetValueOrDefault((id, "noise")) ?? [])
        {
            parameters.Seed = GenerationDataJson.GetSeed(Resolve(noiseNode, "seed"));
            parameters.Width = (int)(GenerationDataJson.GetDouble(Resolve(noiseNode, "width")) ?? 0);
            parameters.Height = (int)(GenerationDataJson.GetDouble(Resolve(noiseNode, "height")) ?? 0);
        }

        return parameters;
    }

    /// <summary>
    /// The value of a node's field, following edges to the node that produces it
    /// </summary>
    private JsonNode? Resolve(string nodeId, string field, int depth = 0)
    {
        if (
            depth < MaxDepth
            && incoming.GetValueOrDefault((nodeId, field)) is [var (sourceNode, sourceField), ..]
        )
            return Resolve(sourceNode, sourceField, depth + 1);

        return nodes.TryGetValue(nodeId, out var node) ? node[field] : null;
    }

    /// <summary>
    /// Walk upstream from a conditioning input (through collect nodes) to the prompt nodes feeding it
    /// </summary>
    private string? FindPrompt(string nodeId, string field)
    {
        var prompts = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(
            (incoming.GetValueOrDefault((nodeId, field)) ?? []).Select(source => source.Node)
        );

        while (queue.Count > 0 && visited.Count < 256)
        {
            var id = queue.Dequeue();
            if (!visited.Add(id) || !nodes.TryGetValue(id, out var node))
                continue;

            if (node.ContainsKey("prompt"))
            {
                if (Trim(GenerationDataJson.GetString(Resolve(id, "prompt"))) is { } prompt)
                    prompts.Add(prompt);
                continue;
            }

            foreach (var ((destNode, destField), sources) in incoming)
            {
                if (destNode != id || NonPromptInputs.Contains(destField))
                    continue;
                foreach (var (sourceNode, _) in sources)
                    queue.Enqueue(sourceNode);
            }
        }

        return prompts.Count == 0 ? null : string.Join(", ", prompts.Distinct());
    }

    private string? FindLoaderModel() =>
        nodes
            .Values.Where(n => Type(n)?.EndsWith("model_loader") ?? false)
            .Select(n => ModelName(n["model"]))
            .FirstOrDefault(name => name is not null);

    private static string? ModelName(JsonNode? model) =>
        GenerationDataJson.GetString(GenerationDataJson.Get(model, "name"))
        ?? GenerationDataJson.GetString(GenerationDataJson.Get(model, "model_name"));

    private static string? Type(JsonObject node) => GenerationDataJson.GetString(node["type"]);

    private static string? Trim(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
