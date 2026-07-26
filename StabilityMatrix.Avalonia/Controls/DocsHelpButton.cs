using System;
using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using StabilityMatrix.Avalonia.Services;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// Contextual help button that opens the in-app documentation viewer at <see cref="DocsPath"/>.
/// Resolves navigation itself so that adding help to a surface needs no view model plumbing.
/// </summary>
/// <remarks>
/// Prefer a <see cref="Core.Models.Documentation.DocumentationPages"/> constant over a literal
/// path so a moved page is fixed in one place.
/// </remarks>
public class DocsHelpButton : Button
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static readonly StyledProperty<string?> DocsPathProperty = AvaloniaProperty.Register<
        DocsHelpButton,
        string?
    >(nameof(DocsPath));

    /// <summary>
    /// Page path relative to the docs root, e.g. <c>advanced/environment-variables.md</c>.
    /// </summary>
    public string? DocsPath
    {
        get => GetValue(DocsPathProperty);
        set => SetValue(DocsPathProperty, value);
    }

    public static readonly StyledProperty<string?> AnchorProperty = AvaloniaProperty.Register<
        DocsHelpButton,
        string?
    >(nameof(Anchor));

    /// <summary>
    /// Optional heading slug within the page to scroll to, e.g. <c>setting-a-variable</c>.
    /// </summary>
    public string? Anchor
    {
        get => GetValue(AnchorProperty);
        set => SetValue(AnchorProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(DocsHelpButton);

    protected override void OnClick()
    {
        base.OnClick();

        if (Design.IsDesignMode)
            return;

        if (string.IsNullOrWhiteSpace(DocsPath))
        {
            Logger.Warn("{Control} clicked with no {Property} set", nameof(DocsHelpButton), nameof(DocsPath));
            return;
        }

        App.Services?.GetService<IDocumentationNavigationService>()?.OpenDocumentation(DocsPath, Anchor);
    }
}
