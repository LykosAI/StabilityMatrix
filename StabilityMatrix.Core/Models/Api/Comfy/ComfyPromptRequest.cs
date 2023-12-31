﻿using System.Text.Json.Serialization;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Core.Models.Api.Comfy;

public class ComfyPromptRequest
{
    [JsonPropertyName("client_id")]
    public required string ClientId { get; set; }
    
    [JsonPropertyName("prompt")]
    public required Dictionary<string, ComfyNode> Prompt { get; set; }
}
