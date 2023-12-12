namespace StabilityMatrix.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class BoolStringMemberAttribute(string trueString, string falseString) : Attribute
{
    public string TrueString { get; } = trueString;
    public string FalseString { get; } = falseString;
}
