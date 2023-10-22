namespace StabilityMatrix.Core.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class TransientAttribute : Attribute
{
    public Type? InterfaceType { get; init; }

    public TransientAttribute() { }

    public TransientAttribute(Type interfaceType)
    {
        InterfaceType = interfaceType;
    }
}
