namespace StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

/// <summary>
/// Combination of the positive and negative conditioning connections.
/// </summary>
public record ConditioningConnections(ConditioningNodeConnection Positive, ConditioningNodeConnection Negative)
{
    // Implicit from tuple
    public static implicit operator ConditioningConnections(
        (ConditioningNodeConnection Positive, ConditioningNodeConnection Negative) value
    ) => new(value.Positive, value.Negative);
}
