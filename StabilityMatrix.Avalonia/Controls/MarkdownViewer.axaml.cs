using System.IO;
using Avalonia;
using Avalonia.Controls.Primitives;
using Markdig;
using TheArtOfDev.HtmlRenderer.Avalonia;

namespace StabilityMatrix.Avalonia.Controls;

public class MarkdownViewer : TemplatedControl
{
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<
        MarkdownViewer,
        string
    >(nameof(Text));

    public string Text
    {
        get => GetValue(TextProperty);
        set
        {
            SetValue(TextProperty, value);
            ParseText(value);
        }
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

    public static readonly StyledProperty<string> CustomCssProperty = AvaloniaProperty.Register<
        MarkdownViewer,
        string
    >(nameof(CustomCss));

    public string CustomCss
    {
        get => GetValue(CustomCssProperty);
        set => SetValue(CustomCssProperty, value);
    }

    private void ParseText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var html =
            $"""<html><body class="markdown-body">{Markdig.Markdown.ToHtml(value, pipeline)}</body></html>""";
        Html = html;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (e.NameScope.Find("PART_HtmlPanel") is not HtmlPanel htmlPanel)
            return;

        using var cssFile = Assets.MarkdownCss.Open();
        using var reader = new StreamReader(cssFile);
        var css = reader.ReadToEnd();

        htmlPanel.BaseStylesheet = $"{css}\n{CustomCss}";

        if (string.IsNullOrWhiteSpace(Html) && !string.IsNullOrWhiteSpace(Text))
        {
            ParseText(Text);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == TextProperty && change.NewValue != null)
        {
            ParseText(change.NewValue.ToString());
        }

        base.OnPropertyChanged(change);
    }
}
