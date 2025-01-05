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
}
