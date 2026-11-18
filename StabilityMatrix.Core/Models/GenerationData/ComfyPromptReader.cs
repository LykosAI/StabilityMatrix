using System.Text.Json.Nodes;

namespace StabilityMatrix.Core.Models.GenerationData;

/// <summary>
/// Reads the prompts and sampler settings of a ComfyUI prompt (the API format node graph).
/// </summary>
internal class ComfyPromptReader
{
    private static readonly string[] SamplerParams = ["sampler_name", "steps", "cfg"];
    private static readonly string[] SamplerTypes = ["WanVideoSampler"];

    /// <summary>
    /// Samplers that take their settings from separate guider, sampler, sigmas and noise nodes
    /// </summary>
    private static readonly string[] CustomSamplerTypes = ["SamplerCustomAdvanced", "SamplerCustom"];
    private static readonly string[] CustomSamplerInputs = ["guider", "sampler", "sigmas", "noise"];

    /// <summary>
    /// Text inputs of prompt nodes: CLIPTextEncode and the text, string and prompt nodes of popular node packs
    /// </summary>
    private static readonly string[] TextKeys =
    [
        "text",
        "Text",
        "prompt",
        "text_g",
        "text_l",
        "value",
        "string",
    ];
    private static readonly string[] PositiveKeys = [.. TextKeys, "positive"];
    private static readonly string[] NegativeKeys = [.. TextKeys, "negative"];

    private static readonly string[] ValueKeys =
    [
        "value",
        "seed",
        "noise_seed",
        "int",
        "float",
        "number",
        "Number",
    ];

    private static readonly string[] ModelKeys = ["ckpt_name", "unet_name", "model"];

    private readonly Dictionary<string, JsonObject> nodes = new(StringComparer.Ordinal);

    private ComfyPromptReader(JsonObject prompt)
    {
        foreach (var (id, node) in prompt)
        {
            if (node is JsonObject obj && obj["class_type"] is not null && obj["inputs"] is JsonObject)
                nodes[id] = obj;
        }
    }

    public static bool IsPrompt(JsonObject? prompt) =>
        prompt is not null
        && prompt.Any(n =>
            n.Value is JsonObject node && node["class_type"] is not null && node["inputs"] is JsonObject
        );

    public static GenerationParameters? Read(JsonObject prompt)
    {
        var reader = new ComfyPromptReader(prompt);
        GenerationParameters? first = null;

        foreach (var (_, node) in reader.nodes)
        {
            if (reader.TryReadSampler(node) is not { } parameters)
                continue;
            first ??= parameters;
            if (!string.IsNullOrEmpty(parameters.PositivePrompt))
                return parameters;
        }

        return first;
    }

    private GenerationParameters? TryReadSampler(JsonObject node)
    {
        var classType = JsonValueText(node["class_type"]);
        if (node["inputs"] is not JsonObject nodeInputs)
            return null;

        var inputs = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (key, value) in nodeInputs)
            inputs[key] = value;

        var isCustomSampler = CustomSamplerTypes.Contains(classType);
        if (isCustomSampler)
        {
            foreach (var name in CustomSamplerInputs)
            {
                if (LinkTarget(inputs.GetValueOrDefault(name)) is not { } targetId)
                    continue;
                if (
                    !nodes.TryGetValue(targetId, out var target)
                    || target["inputs"] is not JsonObject targetInputs
                )
                    continue;
                inputs.Remove(name);
                foreach (var (key, value) in targetInputs)
                    inputs.TryAdd(key, value);
            }
            if (inputs.TryGetValue("noise_seed", out var noiseSeed))
                inputs.TryAdd("seed", noiseSeed);
        }
        else if (!SamplerTypes.Contains(classType) && !SamplerParams.All(inputs.ContainsKey))
        {
            return null;
        }

        var parameters = new GenerationParameters
        {
            Sampler = JsonValueText(
                inputs.GetValueOrDefault("sampler_name") ?? inputs.GetValueOrDefault("scheduler")
            ),
            Steps = (int)(GenerationDataJson.GetDouble(ResolveValue(inputs, "steps")) ?? 0),
            CfgScale = GenerationDataJson.GetDouble(ResolveValue(inputs, "cfg")) ?? 0,
            Seed = GenerationDataJson.GetSeed(
                ResolveValue(inputs, "seed") ?? ResolveValue(inputs, "noise_seed")
            ),
        };

        if (LinkTarget(inputs.GetValueOrDefault("model")) is { } modelId)
            parameters.ModelName = FindModel(modelId);

        // WanVideoSampler takes its prompts from a text embeds node
        var promptInputs = inputs;
        if (
            LinkTarget(inputs.GetValueOrDefault("text_embeds")) is { } embedsId
            && nodes.TryGetValue(embedsId, out var embeds)
            && embeds["inputs"] is JsonObject embedsInputs
        )
        {
            promptInputs = embedsInputs.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        }

        // BasicGuider takes a single conditioning input
        var positiveId = LinkTarget(
            promptInputs.GetValueOrDefault("positive") ?? promptInputs.GetValueOrDefault("conditioning")
        );
        if (positiveId is not null)
            parameters.PositivePrompt = JoinPrompts(FindPrompts(positiveId, PositiveKeys, "negative"));

        if (LinkTarget(promptInputs.GetValueOrDefault("negative")) is { } negativeId)
            parameters.NegativePrompt = JoinPrompts(FindPrompts(negativeId, NegativeKeys, "positive"));

        return parameters;
    }

    /// <summary>
    /// Get an input value, following a link to a primitive or seed node when the value is linked
    /// </summary>
    private JsonNode? ResolveValue(Dictionary<string, JsonNode?> inputs, string key)
    {
        var value = inputs.GetValueOrDefault(key);
        for (var depth = 0; depth < 8 && LinkTarget(value) is { } targetId; depth++)
        {
            if (
                !nodes.TryGetValue(targetId, out var target)
                || target["inputs"] is not JsonObject targetInputs
            )
                return null;
            value =
                targetInputs[key]
                ?? ValueKeys.Select(k => targetInputs[k]).FirstOrDefault(v => v is not null);
        }
        return value is JsonValue ? value : null;
    }

    private string? FindModel(string startId)
    {
        foreach (var (_, node) in Traverse(startId, _ => true))
        {
            if (node["inputs"] is not JsonObject inputs)
                continue;
            foreach (var key in ModelKeys)
            {
                // "model" is also the name of the link input of LoRA loaders and patches: only names count
                if (inputs[key] is JsonValue value && value.TryGetValue<string>(out var name))
                    return name;
            }
        }
        return null;
    }

    /// <summary>
    /// Find the prompts reached from a conditioning input. Nodes that take both conditionings
    /// (WanImageToVideo, ControlNet apply nodes) are only followed through the matching input.
    /// </summary>
    private List<string> FindPrompts(string startId, string[] textKeys, string otherConditioning)
    {
        var prompts = new List<string>();
        foreach (
            var _ in Traverse(startId, inputs => HasPrompt(inputs, textKeys, prompts), otherConditioning)
        ) { }
        return prompts;
    }

    /// <summary>
    /// Collects the prompts of a node; returns whether to keep looking in the node's inputs.
    /// </summary>
    private static bool HasPrompt(JsonObject inputs, string[] textKeys, List<string> prompts)
    {
        var found = false;
        foreach (var key in textKeys)
        {
            if (inputs[key] is JsonValue value && value.TryGetValue<string>(out var text))
            {
                found = true;
                if (!string.IsNullOrWhiteSpace(text))
                    prompts.Add(text.Trim());
            }
        }
        return !found;
    }

    private IEnumerable<(string Id, JsonObject Node)> Traverse(
        string startId,
        Func<JsonObject, bool> descend,
        string? ignoredInput = null
    )
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push(startId);

        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!visited.Add(id) || !nodes.TryGetValue(id, out var node))
                continue;

            yield return (id, node);

            if (node["inputs"] is not JsonObject inputs || !descend(inputs))
                continue;

            var targets = new List<string>();
            foreach (var (name, value) in inputs)
            {
                if (name == "clip" || name == ignoredInput)
                    continue;
                if (
                    value is JsonArray { Count: 2 }
                    && LinkTarget(value) is { } target
                    && nodes.ContainsKey(target)
                )
                    targets.Add(target);
            }
            for (var i = targets.Count - 1; i >= 0; i--)
                stack.Push(targets[i]);
        }
    }

    private static string? LinkTarget(JsonNode? input) =>
        input is JsonArray { Count: > 0 } array ? JsonValueText(array[0]) : null;

    private static string? JsonValueText(JsonNode? node) => GenerationDataJson.GetString(node);

    private static string? JoinPrompts(List<string> prompts) =>
        prompts.Count == 0 ? null : string.Join(", ", prompts.Distinct());
}
