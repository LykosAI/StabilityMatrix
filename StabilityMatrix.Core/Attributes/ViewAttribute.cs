namespace StabilityMatrix.Core.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class ViewAttribute : Attribute
{
    public Type ViewType { get; init; }
    public bool IsPersistent { get; init; }
    
    public ViewAttribute(Type type)
    {
        ViewType = type;
    }
    
    public ViewAttribute(Type type, bool persistent)
    {
        ViewType = type;
        IsPersistent = persistent;
    }
}
