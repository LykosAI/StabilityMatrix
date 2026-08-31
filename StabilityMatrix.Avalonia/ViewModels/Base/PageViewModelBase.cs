using FluentAvalonia.UI.Controls;

namespace StabilityMatrix.Avalonia.ViewModels.Base;

/// <summary>
/// An abstract class for enabling page navigation.
/// </summary>
public abstract class PageViewModelBase : DisposableViewModelBase
{
    /// <summary>
    /// Gets if the user can navigate to the next page
    /// </summary>
    public virtual bool CanNavigateNext { get; protected set; }

    /// <summary>
    /// Gets if the user can navigate to the previous page
    /// </summary>
    public virtual bool CanNavigatePrevious { get; protected set; }

    public abstract string Title { get; }
    public abstract IconSource IconSource { get; }

    /// <summary>
    /// Documentation page this surface links to, as a
    /// <see cref="Core.Models.Documentation.DocumentationPages"/> constant. A shell that hosts
    /// pages renders this as a single help button in its header, so pages declare their own
    /// help target instead of the shell guessing — and no surface ends up with two.
    /// Null means this page has no contextual documentation.
    /// </summary>
    public virtual string? DocsPath => null;
}
