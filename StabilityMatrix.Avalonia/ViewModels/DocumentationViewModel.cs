using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using FluentIcons.Common;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Documentation;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Documentation;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(Views.DocumentationPage))]
[RegisterSingleton<DocumentationViewModel>]
public partial class DocumentationViewModel : PageViewModelBase
{
    private readonly ILogger<DocumentationViewModel> logger;
    private readonly IDocumentationService documentationService;

    public override string Title => "Documentation";

    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.BookOpen, IconVariant = IconVariant.Filled };

    public ObservableCollection<DocumentationSectionNavItem> Sections { get; } = [];

    /// <summary>
    /// Flattened source for the navigation <c>TreeView</c>: root-level pages are hoisted to
    /// top-level leaves and each non-root section is a parent node. Items are either
    /// <see cref="DocumentationSectionNavItem"/> or <see cref="DocumentationPageNavItem"/>.
    /// </summary>
    public ObservableCollection<object> TreeItems { get; } = [];

    [ObservableProperty]
    private DocumentationPageNavItem? selectedPage;

    /// <summary>Two-way bound to the nav TreeView's SelectedItem; routes page leaves to <see cref="SelectedPage"/>.</summary>
    [ObservableProperty]
    private object? selectedTreeItem;

    [ObservableProperty]
    private string? currentMarkdown;

    /// <summary>Raw base URL of the currently displayed page's folder (for relative image resolution).</summary>
    [ObservableProperty]
    private string? currentImageBaseUrl;

    [ObservableProperty]
    private bool isTreeLoading;

    [ObservableProperty]
    private bool isPageLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContentVisible))]
    private bool isDocsUnavailable;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsContentVisible => !IsDocsUnavailable;

    /// <summary>Command bound to the markdown viewer to intercept hyperlink clicks.</summary>
    public ICommand LinkClickedCommand { get; }

    /// <summary>
    /// Raised when an in-page anchor should be scrolled to. The argument is the bare heading slug.
    /// The view subscribes and forwards to the markdown viewer (keeps the VM free of control refs).
    /// </summary>
    public event EventHandler<string>? AnchorRequested;

    private CancellationTokenSource? pageCts;
    private bool hasLoadedTree;

    /// <summary>Anchor slug to scroll to after the next page load completes (cross-page anchor links).</summary>
    private string? pendingAnchor;

    public DocumentationViewModel(
        ILogger<DocumentationViewModel> logger,
        IDocumentationService documentationService
    )
    {
        this.logger = logger;
        this.documentationService = documentationService;
        LinkClickedCommand = new RelayCommand<string>(OnLinkClicked);
    }

    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        if (!hasLoadedTree)
        {
            await LoadTreeAsync();
        }
    }

    [RelayCommand]
    private async Task RetryAsync()
    {
        await LoadTreeAsync(forceRefresh: true);
    }

    private async Task LoadTreeAsync(bool forceRefresh = false)
    {
        IsTreeLoading = true;
        ErrorMessage = null;
        IsDocsUnavailable = false;

        try
        {
            var sections = await documentationService.GetSectionsAsync(forceRefresh);

            Sections.Clear();
            TreeItems.Clear();
            foreach (var section in sections)
            {
                var navSection = new DocumentationSectionNavItem
                {
                    Title = section.Title,
                    Pages = section
                        .Pages.Select(p => new DocumentationPageNavItem { Title = p.Title, Path = p.Path })
                        .ToList(),
                };
                Sections.Add(navSection);

                // Hoist the root (empty-title) section's pages to top-level tree leaves;
                // real sections become parent nodes.
                if (navSection.HasHeader)
                {
                    TreeItems.Add(navSection);
                }
                else
                {
                    foreach (var page in navSection.Pages)
                        TreeItems.Add(page);
                }
            }

            hasLoadedTree = true;

            // Select the first available page (docs README landing page comes first).
            var firstPage = Sections.SelectMany(s => s.Pages).FirstOrDefault();
            if (firstPage is not null)
            {
                SelectedPage = firstPage;
            }
        }
        catch (DocumentationNotAvailableException e)
        {
            logger.LogInformation(e, "Documentation is not available yet");
            IsDocsUnavailable = true;
            Sections.Clear();
            TreeItems.Clear();
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to load documentation tree");
            ErrorMessage = "Could not load the documentation listing. Check your connection and try again.";
            Sections.Clear();
            TreeItems.Clear();
        }
        finally
        {
            IsTreeLoading = false;
        }
    }

    partial void OnSelectedPageChanged(DocumentationPageNavItem? oldValue, DocumentationPageNavItem? newValue)
    {
        // Keep the tree's visual selection in sync (e.g. when navigation comes from a link click).
        SelectedTreeItem = newValue;

        if (newValue is null)
            return;

        // Capture any anchor queued by a cross-page link so it can't race a later navigation.
        var anchor = pendingAnchor;
        pendingAnchor = null;
        LoadPageAsync(newValue.Path, anchor).SafeFireAndForget();
    }

    partial void OnSelectedTreeItemChanged(object? value)
    {
        // Only page leaves load content; selecting a section node is a no-op.
        if (value is DocumentationPageNavItem page)
            SelectedPage = page;
    }

    private async Task LoadPageAsync(string docsRelativePath, string? anchor = null)
    {
        // Replace the CTS before any await so rapid selections can't race on the old one,
        // then cancel the superseded load.
        var oldCts = pageCts;
        var newCts = new CancellationTokenSource();
        pageCts = newCts;
        var ct = newCts.Token;

        if (oldCts is not null)
        {
            await oldCts.CancelAsync();
            oldCts.Dispose();
        }

        IsPageLoading = true;
        ErrorMessage = null;

        try
        {
            var markdown = await documentationService.GetPageMarkdownAsync(
                docsRelativePath,
                cancellationToken: ct
            );
            ct.ThrowIfCancellationRequested();

            CurrentMarkdown = DocumentationPathResolver.RewriteImageUrls(docsRelativePath, markdown);
            CurrentImageBaseUrl = GetPageFolderRawUrl(docsRelativePath);

            // The page content is now set; ask the view to scroll to the requested anchor.
            // The viewer defers the actual measurement to a layout pass, so this can't race
            // the (already-completed) page load.
            if (!string.IsNullOrEmpty(anchor))
                AnchorRequested?.Invoke(this, anchor);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer selection; ignore.
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to load documentation page {Path}", docsRelativePath);
            ErrorMessage = "Could not load this page. Check your connection and try again.";
            CurrentMarkdown = null;
        }
        finally
        {
            IsPageLoading = false;
        }
    }

    private void OnLinkClicked(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return;

        var currentPath = SelectedPage?.Path ?? "README.md";
        var resolved = DocumentationPathResolver.ResolveLink(currentPath, href);

        switch (resolved.Kind)
        {
            case DocumentationPathResolver.LinkKind.External:
                ProcessRunner.OpenUrl(resolved.Target);
                break;

            case DocumentationPathResolver.LinkKind.InternalPage:
                NavigateToPath(resolved.Target, resolved.Fragment);
                break;

            case DocumentationPathResolver.LinkKind.Anchor:
                // Same-page anchor: ask the view to scroll to the heading.
                if (!string.IsNullOrEmpty(resolved.Target))
                    AnchorRequested?.Invoke(this, resolved.Target);
                break;
        }
    }

    private void NavigateToPath(string docsRelativePath, string? fragment = null)
    {
        var match = Sections
            .SelectMany(s => s.Pages)
            .FirstOrDefault(p => string.Equals(p.Path, docsRelativePath, StringComparison.OrdinalIgnoreCase));

        // Already on the target page: SelectedPage won't re-fire, so handle the anchor directly.
        if (match is not null && ReferenceEquals(match, SelectedPage))
        {
            if (!string.IsNullOrEmpty(fragment))
                AnchorRequested?.Invoke(this, fragment);
            return;
        }

        pendingAnchor = fragment;

        if (match is not null)
        {
            SelectedPage = match;
        }
        else
        {
            // Not part of the discovered tree (e.g. a page not yet listed) — load it directly.
            var anchor = pendingAnchor;
            pendingAnchor = null;
            LoadPageAsync(docsRelativePath, anchor).SafeFireAndForget();
        }
    }

    private static string GetPageFolderRawUrl(string docsRelativePath)
    {
        var separatorIndex = docsRelativePath.LastIndexOf('/');
        var folder = separatorIndex < 0 ? string.Empty : docsRelativePath[..(separatorIndex + 1)];
        return DocumentationConstants.GetRawUrl($"{DocumentationConstants.DocsRoot}/{folder}");
    }
}
