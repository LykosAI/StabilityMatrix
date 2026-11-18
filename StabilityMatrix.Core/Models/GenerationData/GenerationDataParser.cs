using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NLog;

namespace StabilityMatrix.Core.Models.GenerationData;

/// <summary>
/// Recognizes the generation data that image generators embed in their images and videos,
/// and reads the prompt and settings where the format allows it.
/// </summary>
public static partial class GenerationDataParser
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private delegate GenerationDataInfo? Parser(MetadataEntries entries);

    /// <summary>
    /// Parsers in order of precedence: specific formats first, since many tools also write
    /// AUTOMATIC1111 parameters or a ComfyUI node graph next to their own data.
    /// </summary>
    private static readonly Parser[] Parsers =
    [
        ParseSwarmUI,
        ParseRuinedFooocus,
        ParseHuggingFaceSpace,
        ParseEasyDiffusion,
        ParseDrawThings,
        ParseDezgo,
        ParseKusArt,
        ParseTensorArt,
        ParseStabilityMatrix,
        ParseCivitai,
        ParseFooocus,
        ParseAutomatic1111,
        ParseComfyUI,
        ParseInvokeAI,
        ParseNovelAI,
    ];

    private static readonly HashSet<string> AiSourceTypes =
    [
        "trainedAlgorithmicMedia",
        "compositeWithTrainedAlgorithmicMedia",
        "compositeSynthetic",
    ];

    public static GenerationDataInfo? TryRead(string path)
    {
        try
        {
            return Parse(MetadataEntries.Read(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Logger.Debug(e, "Failed to read generation data of {Path}", path);
            return null;
        }
        catch (Exception e)
        {
            Logger.Warn(e, "Failed to parse generation data of {Path}", path);
            return null;
        }
    }

    public static GenerationDataInfo? Parse(MetadataEntries entries)
    {
        if (entries.IsEmpty)
            return null;

        // JPEG and WEBP files of AUTOMATIC1111 and Fooocus keep their parameters in the UserComment
        if (
            entries.Container is MetadataContainer.Jpeg or MetadataContainer.Webp
            && entries["UserComment"] is { } userComment
        )
        {
            var userCommentEntries = MetadataEntries.FromDictionary(
                [new KeyValuePair<string, string>("parameters", userComment)],
                entries.Container
            );
            var info = ParseFooocus(userCommentEntries) ?? ParseAutomatic1111(userCommentEntries);
            if (info is not null)
                return WithSoftware(info, entries);
        }

        foreach (var parser in Parsers)
        {
            if (parser(entries) is { } info)
                return WithSoftware(info, entries);
        }

        return ParseProvenance(entries);
    }

    #region Software

    private static GenerationDataInfo WithSoftware(GenerationDataInfo info, MetadataEntries entries)
    {
        if (info.Software is not null)
            return info;

        string? software = null;
        if (entries["smproj"] is not null || entries["parameters-json"] is not null)
            software = "Stability Matrix";
        else if (entries["generation_data"] is not null)
            software = "TensorArt";
        else if (
            (entries["Software"]?.StartsWith("Fooocus") ?? false)
            || entries["fooocus_scheme"] is not null
            || entries["MakerNote"] == "a1111"
        )
            software = "Fooocus";
        else if (info.Source == GenerationDataSource.Automatic1111)
        {
            software = DetectA1111Software(entries["parameters"] ?? entries["UserComment"] ?? string.Empty);
            // ComfyUI save nodes that write AUTOMATIC1111 parameters next to the node graph
            if (software is null && (entries["prompt"]?.Contains("\"class_type\"") ?? false))
                software = "ComfyUI";
        }

        return software is null || software == info.Source.GetDisplayName()
            ? info
            : info with
            {
                Software = software,
            };
    }

    /// <summary>
    /// Name the tool behind an AUTOMATIC1111 style parameter string, from its Version and App fields
    /// </summary>
    public static string? DetectA1111Software(string parameters)
    {
        if (SdNextAppRegex().IsMatch(parameters))
            return "SD.Next";
        if (parameters.Contains("Civitai resources:") || parameters.Contains("Civitai metadata:"))
            return "Civitai";

        // a standalone "Version:" field, not one with a prefix like "ADetailer version:"
        var match = A1111VersionRegex().Match(parameters);
        if (!match.Success)
            return null;
        var version = match.Groups[1].Value;

        if (Regex.IsMatch(version, @"^v\d"))
            return "Stable Diffusion WebUI";
        if (version == "classic")
            return "Forge Classic";
        if (version.StartsWith("neo"))
            return "Forge Neo";
        if (Regex.IsMatch(version, @"^f\d+\.\d+(?:\.\d+)?-v"))
            return "EasyReforge";
        if (Regex.IsMatch(version, @"^f\d+\.\d+(?:\.\d+)?v\d+-v"))
            return "reForge";
        if (Regex.IsMatch(version, @"^f\d"))
            return "Forge";
        if (version.StartsWith("Fooocus"))
            return "Fooocus";
        if (version == "ComfyUI")
            return "ComfyUI";
        return null;
    }

    #endregion

    #region Parsers

    private static GenerationDataInfo Result(GenerationDataSource source, GenerationParameters? parameters) =>
        new() { Source = source, Parameters = parameters };

    private static GenerationParameters FromJson(
        JsonObject json,
        string prompt,
        string negativePrompt,
        string? model,
        string seed,
        string steps,
        string cfgScale,
        string? sampler
    )
    {
        return new GenerationParameters
        {
            PositivePrompt = TrimToNull(GenerationDataJson.GetString(json[prompt])),
            NegativePrompt = TrimToNull(GenerationDataJson.GetString(json[negativePrompt])),
            ModelName = model is null ? null : GenerationDataJson.GetString(json[model]),
            Seed = GenerationDataJson.GetSeed(json[seed]),
            Steps = (int)(GenerationDataJson.GetDouble(json[steps]) ?? 0),
            CfgScale = GenerationDataJson.GetDouble(json[cfgScale]) ?? 0,
            Sampler = sampler is null ? null : GenerationDataJson.GetString(json[sampler]),
            Width = (int)(GenerationDataJson.GetDouble(json["width"]) ?? 0),
            Height = (int)(GenerationDataJson.GetDouble(json["height"]) ?? 0),
        };
    }

    private static GenerationDataInfo? ParseSwarmUI(MetadataEntries entries)
    {
        var source = new[] { entries["parameters"], entries["UserComment"] }.FirstOrDefault(v =>
            v?.Contains("sui_image_params") ?? false
        );
        if (GenerationDataJson.ParseObject(source)?["sui_image_params"] is not JsonObject json)
            return null;

        return Result(
            GenerationDataSource.SwarmUI,
            FromJson(json, "prompt", "negativeprompt", "model", "seed", "steps", "cfgscale", "sampler")
        );
    }

    private static GenerationDataInfo? ParseRuinedFooocus(MetadataEntries entries)
    {
        var source = entries["parameters"] ?? entries["UserComment"];
        if (source is null || !source.Contains("RuinedFooocus"))
            return null;
        if (GenerationDataJson.ParseObject(source) is not { } json)
            return null;
        if (GenerationDataJson.GetString(json["software"]) != "RuinedFooocus")
            return null;

        return Result(
            GenerationDataSource.RuinedFooocus,
            FromJson(json, "Prompt", "Negative", "base_model_name", "seed", "steps", "cfg", "sampler_name")
        );
    }

    private static GenerationDataInfo? ParseHuggingFaceSpace(MetadataEntries entries)
    {
        var json = GenerationDataJson.ParseObject(entries["parameters"] ?? entries["UserComment"]);
        if (
            json is null
            || !json.ContainsKey("resolution")
            || !json.ContainsKey("Model")
            || !json.ContainsKey("prompt")
        )
            return null;

        var parameters = FromJson(
            json,
            "prompt",
            "negative_prompt",
            "Model",
            "seed",
            "num_inference_steps",
            "guidance_scale",
            "sampler"
        );
        parameters.ModelHash = GenerationDataJson.GetString(json["Model hash"]);
        return Result(GenerationDataSource.HuggingFaceSpace, parameters);
    }

    private static GenerationDataInfo? ParseEasyDiffusion(MetadataEntries entries)
    {
        const string marker = "use_stable_diffusion_model";

        JsonObject? json;
        if (entries[marker] is not null)
        {
            // PNG files hold one text chunk per setting, as Python literals
            json = new JsonObject();
            foreach (var (key, value) in entries.Entries)
                json[key] = value;
        }
        else
        {
            var source = new[] { entries["UserComment"], entries["parameters"] }.FirstOrDefault(v =>
                v?.Contains(marker) ?? false
            );
            json = GenerationDataJson.ParseObject(source);
        }
        if (json is null || !json.ContainsKey(marker))
            return null;

        var parameters = FromJson(
            json,
            "prompt",
            "negative_prompt",
            marker,
            "seed",
            "num_inference_steps",
            "guidance_scale",
            "sampler_name"
        );
        if (parameters.NegativePrompt == "None")
            parameters.NegativePrompt = null;
        return Result(GenerationDataSource.EasyDiffusion, parameters);
    }

    private static GenerationDataInfo? ParseDrawThings(MetadataEntries entries)
    {
        if (!(entries["CreatorTool"]?.StartsWith("Draw Things") ?? false))
            return null;
        var json = GenerationDataJson.ParseObject(entries["UserComment"]);
        if (json is null || (!json.ContainsKey("c") && !json.ContainsKey("sampler")))
            return null;

        return Result(
            GenerationDataSource.DrawThings,
            FromJson(json, "c", "uc", "model", "seed", "steps", "scale", "sampler")
        );
    }

    private static GenerationDataInfo? ParseDezgo(MetadataEntries entries)
    {
        JsonObject? job = null;
        if (entries["GenInfo"] is { } genInfo)
        {
            job = GenerationDataJson.ParseObject(genInfo);
        }
        else if (entries["UserComment"] is { } userComment && DezgoJobRegex().IsMatch(userComment))
        {
            job = SimpleYaml.Parse(userComment);
        }
        if (job?["Input"] is not JsonObject input || job["JobId"] is null)
            return null;

        return Result(
            GenerationDataSource.Dezgo,
            FromJson(input, "prompt", "negative_prompt", "model", "seed", "steps", "guidance", "sampler")
        );
    }

    private static GenerationDataInfo? ParseKusArt(MetadataEntries entries)
    {
        if (entries["Copyright"] is not { } copyright || !KusArtRegex().IsMatch(copyright))
            return null;
        if (GenerationDataJson.ParseObject(entries["extra"]) is not { } json)
            return null;

        return Result(
            GenerationDataSource.KusArt,
            new GenerationParameters
            {
                PositivePrompt = TrimToNull(GenerationDataJson.GetString(json["prompt"])),
                NegativePrompt = TrimToNull(GenerationDataJson.GetString(json["negative_prompt"])),
            }
        );
    }

    private static GenerationDataInfo? ParseTensorArt(MetadataEntries entries)
    {
        if (GenerationDataJson.ParseObject(entries["generation_data"]) is not { } json)
            return null;

        var parameters = FromJson(
            json,
            "prompt",
            "negativePrompt",
            null,
            "seed",
            "steps",
            "cfgScale",
            json.ContainsKey("ksamplerName") ? "ksamplerName" : "samplerName"
        );
        if (json["baseModel"] is JsonObject baseModel)
        {
            parameters.ModelName = GenerationDataJson.GetString(baseModel["modelFileName"]);
            parameters.ModelHash = GenerationDataJson.GetString(baseModel["hash"]);
        }

        // -1 is written for random seeds, the node graph has the real one
        if (
            GenerationDataJson.GetDouble(json["seed"]) is -1
            && GenerationDataJson.ParseObject(entries["prompt"]) is { } graph
        )
        {
            foreach (var (_, node) in graph)
            {
                var inputs = GenerationDataJson.Get(node, "inputs");
                var seed =
                    GenerationDataJson.Get(inputs, "seed") ?? GenerationDataJson.Get(inputs, "noise_seed");
                if (seed is JsonValue value && value.GetValueKind() == JsonValueKind.Number)
                {
                    parameters.Seed = GenerationDataJson.GetSeed(seed);
                    break;
                }
            }
        }

        return Result(GenerationDataSource.TensorArt, parameters);
    }

    private static GenerationDataInfo? ParseStabilityMatrix(MetadataEntries entries)
    {
        var json = entries["parameters-json"];
        // WEBP files written by Stability Matrix keep the parameters in the ImageDescription
        if (
            json is null
            && entries["smproj"] is null
            && entries.Container == MetadataContainer.Webp
            && entries["ImageDescription"] is { } description
            && description.Contains("\"PositivePrompt\"")
        )
        {
            json = description;
        }
        if (json is null)
            return null;

        GenerationParameters? parameters;
        try
        {
            parameters = JsonSerializer.Deserialize<GenerationParameters>(json);
        }
        catch (JsonException)
        {
            return null;
        }

        return parameters is null ? null : Result(GenerationDataSource.StabilityMatrix, parameters);
    }

    private static GenerationDataInfo? ParseCivitai(MetadataEntries entries)
    {
        var source = new[]
        {
            entries["UserComment"],
            entries["prompt"],
            entries["parameters"],
        }.FirstOrDefault(v => v?.Contains("\"extraMetadata\"") ?? false);
        if (GenerationDataJson.ParseObject(source) is not { } graph)
            return null;
        if (GenerationDataJson.AsObject(graph["extraMetadata"]) is not { } json)
            return null;

        var parameters = FromJson(
            json,
            "prompt",
            "negativePrompt",
            null,
            "seed",
            "steps",
            "cfgScale",
            "sampler"
        );
        foreach (var (_, node) in graph)
        {
            if (
                GenerationDataJson.Get(GenerationDataJson.Get(node, "inputs"), "ckpt_name")
                    is JsonValue checkpoint
                && checkpoint.TryGetValue<string>(out var name)
            )
            {
                parameters.ModelName = name;
                break;
            }
        }

        return Result(GenerationDataSource.Civitai, parameters);
    }

    private static GenerationDataInfo? ParseFooocus(MetadataEntries entries)
    {
        var json = GenerationDataJson.ParseObject(entries["parameters"]);
        if (json is null || json.ContainsKey("num_inference_steps"))
            return null;
        if (!json.ContainsKey("sampler") || !json.ContainsKey("prompt"))
            return null;

        var parameters = FromJson(
            json,
            "prompt",
            "negative_prompt",
            "base_model",
            "seed",
            "steps",
            "guidance_scale",
            "sampler"
        );
        parameters.ModelHash = GenerationDataJson.GetString(json["base_model_hash"]);
        return Result(GenerationDataSource.Fooocus, parameters);
    }

    private static GenerationDataInfo? ParseAutomatic1111(MetadataEntries entries)
    {
        // CivitAI writes the parameters to the EXIF UserComment of its PNG files
        var text = entries["parameters"] ?? entries["UserComment"];
        if (text is null)
            return null;

        var lines = text.Replace("\r\n", "\n").Split('\n');

        // the settings line: searched from the end, it names at least three sampler settings
        var infoIndex = -1;
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var count = A1111SamplerKeyRegex()
                .Matches(lines[i])
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .Count();
            if (count >= 3)
            {
                infoIndex = i;
                break;
            }
        }
        if (infoIndex < 0)
            return null;

        var promptLines = lines[..infoIndex];
        // SD.Next repeats the prompts as templates before the settings
        var templateIndex = Array.FindIndex(promptLines, l => A1111TemplateRegex().IsMatch(l));
        if (templateIndex > 0)
            promptLines = promptLines[..templateIndex];

        var prompts = string.Join('\n', promptLines).Split("Negative prompt:");
        if (prompts.Length > 2)
            return null;

        var infoLine = lines[infoIndex];
        if (
            !infoLine.StartsWith("Steps:")
            || !GenerationParameters.TryParse(
                string.Join('\n', [.. promptLines, infoLine]),
                out var parameters
            )
        )
        {
            var fields = GenerationParameters.ParseLine(infoLine);
            parameters = new GenerationParameters
            {
                Steps = ParseInt(fields.GetValueOrDefault("Steps")),
                Sampler = fields.GetValueOrDefault("Sampler"),
                CfgScale = ParseDouble(fields.GetValueOrDefault("CFG scale")),
                Seed = ulong.TryParse(fields.GetValueOrDefault("Seed"), out var seed) ? seed : 0,
                ModelName = fields.GetValueOrDefault("Model"),
                ModelHash = fields.GetValueOrDefault("Model hash"),
            };
            if (fields.GetValueOrDefault("Size")?.Split('x', 2) is [var width, var height])
            {
                parameters.Width = ParseInt(width);
                parameters.Height = ParseInt(height);
            }
        }

        parameters.PositivePrompt = TrimToNull(prompts[0]);
        parameters.NegativePrompt = prompts.Length > 1 ? TrimToNull(prompts[1]) : null;
        return Result(GenerationDataSource.Automatic1111, parameters);
    }

    private static GenerationDataInfo? ParseComfyUI(MetadataEntries entries)
    {
        var prompt = GenerationDataJson.ParseObject(entries["prompt"]);
        if (!ComfyPromptReader.IsPrompt(prompt))
            return null;

        return Result(GenerationDataSource.ComfyUI, ComfyPromptReader.Read(prompt!));
    }

    private static GenerationDataInfo? ParseInvokeAI(MetadataEntries entries)
    {
        if (entries["sd-metadata"] is { } sdMetadata)
        {
            if (GenerationDataJson.ParseObject(sdMetadata) is not { } metadata)
                return null;
            if (metadata["image"] is not JsonObject image || image["prompt"] is not JsonArray promptList)
                return null;

            var parameters = FromJson(image, "_", "_", null, "seed", "steps", "cfg_scale", "sampler");
            parameters.ModelName = GenerationDataJson.GetString(metadata["model_weights"]);
            parameters.ModelHash = GenerationDataJson.GetString(metadata["model_hash"]);
            var combined = string.Join(
                ' ',
                promptList
                    .Select(p => GenerationDataJson.GetString(GenerationDataJson.Get(p, "prompt")))
                    .OfType<string>()
            );
            (parameters.PositivePrompt, parameters.NegativePrompt) = SplitInvokePrompt(combined);
            return Result(GenerationDataSource.InvokeAI, parameters);
        }

        // legacy "Dream" format: "prompt [negative]" -s 50 -S 42 -W 512 -H 512 -C 7.5 -A k_lms
        if (entries["Dream"] is { } dream)
        {
            var match = InvokeDreamRegex().Match(dream);
            if (!match.Success)
                return null;

            var args = InvokeDreamArgRegex()
                .Matches(match.Groups[2].Value)
                .GroupBy(m => m.Groups[1].Value)
                .ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
            var parameters = new GenerationParameters
            {
                Sampler = args.GetValueOrDefault("A"),
                CfgScale = ParseDouble(args.GetValueOrDefault("C")),
                Steps = ParseInt(args.GetValueOrDefault("s")),
                Seed = ulong.TryParse(args.GetValueOrDefault("S"), out var seed) ? seed : 0,
                Width = ParseInt(args.GetValueOrDefault("W")),
                Height = ParseInt(args.GetValueOrDefault("H")),
            };
            (parameters.PositivePrompt, parameters.NegativePrompt) = SplitInvokePrompt(match.Groups[1].Value);
            return Result(GenerationDataSource.InvokeAI, parameters);
        }

        if (
            GenerationDataJson.ParseObject(entries["invokeai_metadata"]) is { } json
            && json.ContainsKey("scheduler")
        )
        {
            var parameters = FromJson(
                json,
                "positive_prompt",
                "negative_prompt",
                null,
                "seed",
                "steps",
                "cfg_scale",
                "scheduler"
            );
            if (json["model"] is JsonObject model)
            {
                parameters.ModelName =
                    GenerationDataJson.GetString(model["model_name"])
                    ?? GenerationDataJson.GetString(model["name"]);
            }
            return Result(GenerationDataSource.InvokeAI, parameters);
        }

        // Some tools only keep the workflow graph
        if (
            InvokeGraphReader.Read(GenerationDataJson.ParseObject(entries["invokeai_graph"])) is
            { } graphParameters
        )
        {
            return Result(GenerationDataSource.InvokeAI, graphParameters);
        }

        return null;
    }

    /// <summary>
    /// Legacy InvokeAI prompts hold the negative prompt in square brackets
    /// </summary>
    private static (string? Positive, string? Negative) SplitInvokePrompt(string combined)
    {
        var negatives = new List<string>();
        var positive = InvokeNegativeRegex()
            .Replace(
                combined,
                m =>
                {
                    negatives.Add(m.Groups[1].Value.Trim(' ', ','));
                    return " ";
                }
            );
        return (
            TrimToNull(Regex.Replace(positive, @"\s+", " ").Trim(' ', ',')),
            TrimToNull(string.Join(", ", negatives.Where(n => n.Length > 0)))
        );
    }

    private static GenerationDataInfo? ParseNovelAI(MetadataEntries entries)
    {
        if (entries["Description"] is not { } description || entries["Source"] is not { } source)
            return null;
        if (
            GenerationDataJson.ParseObject(entries["Comment"]) is not { } json
            || !json.ContainsKey("sampler")
        )
            return null;

        var parameters = FromJson(json, "prompt", "uc", null, "seed", "steps", "scale", "sampler");
        // WEBP files can carry a Description with leading null characters or broken UTF-8
        var cleaned = description.TrimStart('\0').Trim();
        if (!cleaned.Contains('�') || parameters.PositivePrompt is null)
            parameters.PositivePrompt = TrimToNull(cleaned);

        var model = NovelAIModelRegex().Match(source);
        if (model.Success)
        {
            parameters.ModelName = model.Groups[1].Value;
            parameters.ModelHash = model.Groups[2].Value;
        }
        return Result(GenerationDataSource.NovelAI, parameters);
    }

    /// <summary>
    /// Images labelled as AI made that carry no generation data, like those of ChatGPT or Gemini.
    /// The labels are not verified.
    /// </summary>
    private static GenerationDataInfo? ParseProvenance(MetadataEntries entries)
    {
        if (entries.C2pa is { Length: > 0 } c2pa)
        {
            var text = Encoding.Latin1.GetString(c2pa);
            if (AiSourceTypes.Any(text.Contains))
            {
                return new GenerationDataInfo
                {
                    Source = GenerationDataSource.C2pa,
                    Software = C2paSoftware(c2pa, text),
                };
            }
        }

        var sourceType = entries["DigitalSourceType"];
        if (sourceType is not null && AiSourceTypes.Contains(sourceType.Split('/').Last()))
        {
            return new GenerationDataInfo
            {
                Source = GenerationDataSource.Iptc,
                Software = entries["CreatorTool"] ?? VendorSoftware(Vendor(entries["Publisher"])),
            };
        }

        return null;
    }

    private static readonly (Regex Pattern, string Vendor)[] Vendors =
    [
        (new Regex("openai", RegexOptions.IgnoreCase), "OpenAI"),
        (new Regex("google", RegexOptions.IgnoreCase), "Google"),
        (new Regex("adobe", RegexOptions.IgnoreCase), "Adobe"),
        (new Regex("microsoft", RegexOptions.IgnoreCase), "Microsoft"),
        (new Regex(@"\bxai\b|spacexai|grok", RegexOptions.IgnoreCase), "xAI"),
        (new Regex(@"\bmeta\b|facebook|instagram", RegexOptions.IgnoreCase), "Meta"),
        (new Regex("stability", RegexOptions.IgnoreCase), "Stability AI"),
        (new Regex("midjourney", RegexOptions.IgnoreCase), "Midjourney"),
        (new Regex("runway", RegexOptions.IgnoreCase), "Runway"),
        (new Regex("black forest labs", RegexOptions.IgnoreCase), "Black Forest Labs"),
    ];

    private static string? Vendor(string? text) =>
        text is null ? null : Vendors.FirstOrDefault(v => v.Pattern.IsMatch(text)).Vendor;

    private static string? VendorSoftware(string? vendor) =>
        vendor switch
        {
            "Google" => "Gemini",
            "Meta" => "Meta AI",
            "OpenAI" => "ChatGPT",
            _ => vendor,
        };

    /// <summary>
    /// Name the tool of a C2PA manifest from its software agent, or else its signer
    /// </summary>
    private static string? C2paSoftware(byte[] manifest, string text)
    {
        if (ReadC2paSoftwareAgent(manifest) is { Length: > 0 } name)
        {
            if (Regex.IsMatch(name, @"^(?:chatgpt\b|gpt-image|dall-e)", RegexOptions.IgnoreCase))
                return "ChatGPT";
            return Regex.Replace(name, @"\s+\d+(?:\.\d+)*$", "");
        }
        return VendorSoftware(Vendor(text));
    }

    /// <summary>
    /// Find the first "softwareAgent" of the CBOR encoded actions, either a text or a map with a name
    /// </summary>
    private static string? ReadC2paSoftwareAgent(byte[] manifest)
    {
        var key = "\u006DsoftwareAgent"u8;
        var index = manifest.AsSpan().IndexOf(key);
        if (index < 0)
            return null;
        var offset = index + key.Length;

        if (offset < manifest.Length && manifest[offset] is >= 0xA0 and <= 0xB7)
        {
            var nameKey = "\u0064name"u8;
            var nameIndex = manifest.AsSpan(offset, Math.Min(256, manifest.Length - offset)).IndexOf(nameKey);
            if (nameIndex < 0)
                return null;
            offset += nameIndex + nameKey.Length;
        }

        return ReadCborText(manifest, offset);
    }

    private static string? ReadCborText(byte[] data, int offset)
    {
        if (offset >= data.Length || data[offset] >> 5 != 3)
            return null;
        var info = data[offset] & 0x1F;
        int length;
        var start = offset + 1;
        if (info < 24)
        {
            length = info;
        }
        else if (info == 24 && start < data.Length)
        {
            length = data[start];
            start += 1;
        }
        else if (info == 25 && start + 1 < data.Length)
        {
            length = (data[start] << 8) | data[start + 1];
            start += 2;
        }
        else
        {
            return null;
        }
        return start + length <= data.Length ? Encoding.UTF8.GetString(data, start, length).Trim() : null;
    }

    #endregion

    #region Helpers

    private static string? TrimToNull(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static int ParseInt(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? (int)value
            : 0;

    private static double ParseDouble(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;

    [GeneratedRegex(@"(?:^|[,\n])\s*App:\s*SD\.Next\b")]
    private static partial Regex SdNextAppRegex();

    [GeneratedRegex(@"(?:^|[,\n])\s*Version:\s*([^\s,]+)")]
    private static partial Regex A1111VersionRegex();

    [GeneratedRegex(@"(?:^|,)\s*(Sampler|CFG scale|Seed|Steps|ENSD|Schedule type):")]
    private static partial Regex A1111SamplerKeyRegex();

    [GeneratedRegex(@"^(?:Negative )?[Tt]emplate:")]
    private static partial Regex A1111TemplateRegex();

    [GeneratedRegex(@"^\s*JobId:", RegexOptions.Multiline)]
    private static partial Regex DezgoJobRegex();

    [GeneratedRegex(@"^Generated by Kus(?:Art|aPics)$")]
    private static partial Regex KusArtRegex();

    [GeneratedRegex(@"^""(.*?)""(.*)$", RegexOptions.Singleline)]
    private static partial Regex InvokeDreamRegex();

    [GeneratedRegex(@"\s+-([\p{L}\p{N}_]+)\s+(\S+)")]
    private static partial Regex InvokeDreamArgRegex();

    [GeneratedRegex(@"\[([^\[]*)\]")]
    private static partial Regex InvokeNegativeRegex();

    [GeneratedRegex(@"^(.*?)\s+([A-Z0-9]+)$")]
    private static partial Regex NovelAIModelRegex();

    #endregion
}
