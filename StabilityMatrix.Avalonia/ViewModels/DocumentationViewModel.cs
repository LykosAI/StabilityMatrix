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

    [ObservableProperty]
    private DocumentationPageNavItem? selectedPage;

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

    private CancellationTokenSource? pageCts;
    private bool hasLoadedTree;

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

    [RelayCommand]
    private void SelectPage(DocumentationPageNavItem? page)
    {
        if (page is not null)
            SelectedPage = page;
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
            foreach (var section in sections)
            {
                Sections.Add(
                    new DocumentationSectionNavItem
                    {
                        Title = section.Title,
                        Pages = section
                            .Pages.Select(p => new DocumentationPageNavItem
                            {
                                Title = p.Title,
                                Path = p.Path,
                            })
                            .ToList(),
                    }
                );
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
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to load documentation tree");
            ErrorMessage = "Could not load the documentation listing. Check your connection and try again.";
            Sections.Clear();
        }
        finally
        {
            IsTreeLoading = false;
        }
    }

    partial void OnSelectedPageChanged(DocumentationPageNavItem? oldValue, DocumentationPageNavItem? newValue)
    {
        if (oldValue is not null)
            oldValue.IsSelected = false;

        if (newValue is null)
            return;

        newValue.IsSelected = true;
        LoadPageAsync(newValue.Path).SafeFireAndForget();
    }

    private async Task LoadPageAsync(string docsRelativePath)
    {
        // Cancel any in-flight page load
        await (pageCts?.CancelAsync() ?? Task.CompletedTask);
        pageCts?.Dispose();
        pageCts = new CancellationTokenSource();
        var ct = pageCts.Token;

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
                NavigateToPath(resolved.Target);
                break;

            case DocumentationPathResolver.LinkKind.Anchor:
                // In-page anchors are not currently supported; no-op.
                break;
        }
    }

    private void NavigateToPath(string docsRelativePath)
    {
        var match = Sections
            .SelectMany(s => s.Pages)
            .FirstOrDefault(p => string.Equals(p.Path, docsRelativePath, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            SelectedPage = match;
        }
        else
        {
            // Not part of the discovered tree (e.g. a page not yet listed) — load it directly.
            LoadPageAsync(docsRelativePath).SafeFireAndForget();
        }
    }

    private static string GetPageFolderRawUrl(string docsRelativePath)
    {
        var separatorIndex = docsRelativePath.LastIndexOf('/');
        var folder = separatorIndex < 0 ? string.Empty : docsRelativePath[..(separatorIndex + 1)];
        return DocumentationConstants.GetRawUrl($"{DocumentationConstants.DocsRoot}/{folder}");
    }
}
