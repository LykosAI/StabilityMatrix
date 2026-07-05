using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<DocumentationPage>]
public partial class DocumentationPage : UserControlBase
{
    public DocumentationPage()
    {
        InitializeComponent();
    }
}
