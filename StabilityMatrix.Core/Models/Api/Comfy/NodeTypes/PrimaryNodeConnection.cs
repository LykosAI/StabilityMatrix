using OneOf;

namespace StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

/// <summary>
/// Union for the primary Image or Latent node connection
/// </summary>
[GenerateOneOf]
public partial class PrimaryNodeConnection
    : OneOfBase<LatentNodeConnection, ImageNodeConnection> { }
