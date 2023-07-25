namespace StabilityMatrix.Core.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class ViewAttribute : Attribute
{
    public ViewAttribute(Type type)
    {
        this.type = type;
    }

    private readonly Type type;
    public Type GetViewType() => type;
}
