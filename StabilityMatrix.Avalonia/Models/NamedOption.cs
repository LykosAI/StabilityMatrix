namespace StabilityMatrix.Avalonia.Models;

public class NamedOption<T>(string label, T value)
{
    public string Label { get; } = label;
    public T Value { get; } = value;

    public override string ToString() => Label;
}
