namespace StabilityMatrix.Core.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class SingletonAttribute : Attribute
{
    public Type? InterfaceType { get; init; }

    public SingletonAttribute() { }

    public SingletonAttribute(Type interfaceType)
    {
        InterfaceType = interfaceType;
    }
}
