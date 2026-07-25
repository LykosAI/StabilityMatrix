using Avalonia.Controls;
using Avalonia.VisualTree;
using FluentIcons.Avalonia.Fluent;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Models.Documentation;

namespace StabilityMatrix.UITests;

[Collection("TempDir")]
public class DocsHelpButtonTests : TestBase
{
    /// <summary>
    /// The control theme lives in a merged resource dictionary, so a missing or misnamed
    /// registration compiles fine and only shows up as an invisible button at runtime.
    /// </summary>
    [AvaloniaFact]
    public void DocsHelpButton_ShouldResolveControlTheme()
    {
        var button = new DocsHelpButton { DocsPath = DocumentationPages.EnvironmentVariables };
        var window = new Window { Content = button };

        window.Show();

        var icon = button.GetVisualDescendants().OfType<SymbolIcon>().FirstOrDefault();

        Assert.NotNull(icon);
        Assert.Equal("Hand", button.Cursor?.ToString());
    }
}
