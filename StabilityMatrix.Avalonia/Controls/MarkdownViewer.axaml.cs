using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls.Primitives;
using Markdig;
using TheArtOfDev.HtmlRenderer.Avalonia;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// Renders Markdown, or HTML set through the Html property, with the app's markdown stylesheet.
/// </summary>
public class MarkdownViewer : TemplatedControlBase
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseEmojiAndSmiley()
        .Build();

    private HtmlLabel? htmlLabel;

    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<
        MarkdownViewer,
        string
    >(nameof(Text));

    /// <summary>
    /// Markdown to render.
    /// </summary>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly StyledProperty<string> HtmlProperty = AvaloniaProperty.Register<
        MarkdownViewer,
        string
    >(nameof(Html));

    private string Html
    {
        get => GetValue(HtmlProperty);
        set => SetValue(HtmlProperty, value);
    }

    public static readonly StyledProperty<bool> FitToContentProperty =
        MarkdownHtmlLabel.FitToContentProperty.AddOwner<MarkdownViewer>();

    /// <summary>
    /// Size the width to the text, up to the width offered, instead of filling it. For chat
    /// bubbles; leave it off where the content should span its container.
    /// </summary>
    public bool FitToContent
    {
        get => GetValue(FitToContentProperty);
        set => SetValue(FitToContentProperty, value);
    }

    public static readonly StyledProperty<string> CustomCssProperty = AvaloniaProperty.Register<
        MarkdownViewer,
        string
    >(nameof(CustomCss));

    /// <summary>
    /// CSS appended to the markdown stylesheet when the template is applied.
    /// </summary>
    public string CustomCss
    {
        get => GetValue(CustomCssProperty);
        set => SetValue(CustomCssProperty, value);
    }

    /// <summary>
    /// Encodes every character outside the Basic Multilingual Plane (emoji, mostly) as a numeric
    /// character reference. HtmlRenderer splits words at each UTF-16 code unit in U+4E00..U+FA2D,
    /// a range that covers the surrogates, so a raw emoji is drawn as two U+FFFD halves; a
    /// reference is decoded after that split and keeps the pair together.
    /// </summary>
    internal static string? EncodeAstralCharacters(string? html)
    {
        if (string.IsNullOrEmpty(html) || html.AsSpan().IndexOfAnyInRange('\uD800', '\uDFFF') < 0)
            return html;

        var builder = new StringBuilder(html.Length + 16);
        foreach (var rune in html.EnumerateRunes())
        {
            if (rune.IsBmp)
                builder.Append((char)rune.Value);
            else
                builder.Append("&#x").Append(rune.Value.ToString("X")).Append(';');
        }

        return builder.ToString();
    }

    private void ParseText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var html =
            $"""<html><body class="markdown-body">{Markdig.Markdown.ToHtml(value, Pipeline)}</body></html>""";
        Html = html;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        htmlLabel = e.NameScope.Find("PART_HtmlLabel") as HtmlLabel;
        if (htmlLabel is null)
            return;

        using var cssFile = Assets.MarkdownCss.Open();
        using var reader = new StreamReader(cssFile);
        var css = reader.ReadToEnd();

        htmlLabel.BaseStylesheet = $"{css}\n{CustomCss}";

        if (string.IsNullOrWhiteSpace(Html) && !string.IsNullOrWhiteSpace(Text))
        {
            ParseText(Text);
        }
        else
        {
            htmlLabel.Text = EncodeAstralCharacters(Html);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == TextProperty && change.NewValue != null)
        {
            ParseText(change.NewValue.ToString());
        }
        else if (change.Property == HtmlProperty && htmlLabel is not null)
        {
            htmlLabel.Text = EncodeAstralCharacters(change.NewValue as string);
        }

        base.OnPropertyChanged(change);
    }
}
