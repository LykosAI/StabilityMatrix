using System;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.ViewModels;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Single entry point for every contextual help affordance in the app, so that call sites
/// only name the docs page they want and never touch the navigation frame or the viewer's state.
/// </summary>
[RegisterSingleton<IDocumentationNavigationService, DocumentationNavigationService>]
public class DocumentationNavigationService(
    INavigationService<MainWindowViewModel> navigationService,
    Lazy<DocumentationViewModel> documentationViewModel
) : IDocumentationNavigationService
{
    /// <inheritdoc />
    public void OpenDocumentation(string? docsRelativePath = null, string? anchor = null)
    {
        // Queue the target before navigating: on the viewer's first load the nav tree would
        // otherwise select its landing page after we navigate, discarding the request.
        if (!string.IsNullOrWhiteSpace(docsRelativePath))
        {
            documentationViewModel.Value.RequestPage(docsRelativePath, anchor);
        }

        navigationService.NavigateTo<DocumentationViewModel>(new BetterEntranceNavigationTransition());
    }
}
