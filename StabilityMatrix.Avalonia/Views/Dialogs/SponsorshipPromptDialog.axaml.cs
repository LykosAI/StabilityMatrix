using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<SponsorshipPromptDialog>]
public partial class SponsorshipPromptDialog : UserControlBase
{
    public SponsorshipPromptDialog()
    {
        InitializeComponent();
    }
}
