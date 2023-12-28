using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using JetBrains.Annotations;
using StabilityMatrix.Avalonia.Models;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// Selector for objects implementing <see cref="ITemplateKey{T}"/>
/// </summary>
[PublicAPI]
public class DataTemplateSelector<TKey> : IDataTemplate
    where TKey : notnull
{
    /// <summary>
    /// Key that is used when no other key matches
    /// </summary>
    public TKey? DefaultKey { get; set; }

    [Content]
    public Dictionary<TKey, IDataTemplate> Templates { get; } = new();

    public bool Match(object? data) => data is ITemplateKey<TKey>;

    /// <inheritdoc />
    public Control Build(object? data)
    {
        if (data is not ITemplateKey<TKey> key)
            throw new ArgumentException(null, nameof(data));

        if (Templates.TryGetValue(key.TemplateKey, out var template))
        {
            return template.Build(data)!;
        }

        if (DefaultKey is not null && Templates.TryGetValue(DefaultKey, out var defaultTemplate))
        {
            return defaultTemplate.Build(data)!;
        }

        throw new ArgumentException(null, nameof(data));
    }
}
