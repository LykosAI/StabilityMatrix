using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Core.Models.Documentation;

namespace StabilityMatrix.UITests;

[Collection("TempDir")]
public class AutomationNameTests : TestBase
{
    /// <summary>
    /// The pane toggle is named via a /template/ style selector in MainWindow.axaml, which
    /// silently matches nothing if FluentAvalonia renames the template part.
    /// </summary>
    [AvaloniaFact]
    public async Task NavigationTogglePaneButton_ShouldHaveAutomationName()
    {
        var (window, _) = GetMainWindow();
        await DoInitialSetup();

        var toggleButton = window
            .FindDescendantOfType<NavigationView>()!
            .GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => b.Name == "TogglePaneButton");

        Assert.NotNull(toggleButton);
        Assert.Equal("Toggle Navigation", AutomationProperties.GetName(toggleButton));
    }

    /// <summary>
    /// Icon-only control: the accessible name comes from the control theme, not content.
    /// </summary>
    [AvaloniaFact]
    public void DocsHelpButton_ShouldHaveAutomationName()
    {
        var button = new DocsHelpButton { DocsPath = DocumentationPages.EnvironmentVariables };
        var window = new Window { Content = button };

        window.Show();

        Assert.Equal(Resources.Label_ViewDocumentation, AutomationProperties.GetName(button));
    }
}
