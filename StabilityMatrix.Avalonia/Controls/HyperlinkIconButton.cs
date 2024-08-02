using System;
using System.Diagnostics;
using System.IO;
using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Logging;
using StabilityMatrix.Core.Processes;
using Symbol = FluentIcons.Common.Symbol;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// Like <see cref="HyperlinkButton"/>, but with a link icon left of the text content.
/// </summary>
public class HyperlinkIconButton : Button
{
    private Uri? _navigateUri;

    /// <summary>
    /// Defines the <see cref="NavigateUri"/> property
    /// </summary>
    public static readonly DirectProperty<HyperlinkIconButton, Uri?> NavigateUriProperty =
        AvaloniaProperty.RegisterDirect<HyperlinkIconButton, Uri?>(
            nameof(NavigateUri),
            x => x.NavigateUri,
            (x, v) => x.NavigateUri = v
        );

    /// <summary>
    /// Gets or sets the Uri that the button should navigate to upon clicking. In assembly paths are not supported, (e.g., avares://...)
    /// </summary>
    public Uri? NavigateUri
    {
        get => _navigateUri;
        set => SetAndRaise(NavigateUriProperty, ref _navigateUri, value);
    }

    public static readonly StyledProperty<Symbol> IconProperty = AvaloniaProperty.Register<
        HyperlinkIconButton,
        Symbol
    >("Icon", Symbol.Link);

    public Symbol Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(HyperlinkIconButton);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Update icon
        if (change.Property == NavigateUriProperty)
        {
            var uri = change.GetNewValue<Uri?>();

            if (uri is not null && uri.IsFile && Icon == Symbol.Link)
            {
                Icon = Symbol.Open;
            }
        }
    }

    protected override void OnClick()
    {
        base.OnClick();

        if (NavigateUri is null)
            return;

        // File or Folder URIs
        if (NavigateUri.IsFile)
        {
            var path = NavigateUri.LocalPath;

            if (Directory.Exists(path))
            {
                ProcessRunner
                    .OpenFolderBrowser(path)
                    .SafeFireAndForget(ex =>
                    {
                        Logger.TryGet(LogEventLevel.Error, $"Unable to open directory Uri {NavigateUri}");
                    });
            }
            else if (File.Exists(path))
            {
                ProcessRunner
                    .OpenFileBrowser(path)
                    .SafeFireAndForget(ex =>
                    {
                        Logger.TryGet(LogEventLevel.Error, $"Unable to open file Uri {NavigateUri}");
                    });
            }
        }
        // Web
        else
        {
            try
            {
                Process.Start(
                    new ProcessStartInfo(NavigateUri.ToString()) { UseShellExecute = true, Verb = "open" }
                );
            }
            catch
            {
                Logger.TryGet(LogEventLevel.Error, $"Unable to open Uri {NavigateUri}");
            }
        }
    }

    protected override bool RegisterContentPresenter(ContentPresenter presenter)
    {
        return presenter.Name == "ContentPresenter";
    }
}
