namespace StabilityMatrix.Avalonia.Models;

/// <summary>
/// Implements a template key for <see cref="DataTemplateSelector"/>
/// </summary>
public interface ITemplateKey<out T>
{
    T TemplateKey { get; }
}
