using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;

namespace StabilityMatrix.Helper;

public class DialogFactory : IDialogFactory
{
    private readonly IContentDialogService contentDialogService;
    private readonly LaunchOptionsDialogViewModel launchOptionsDialogViewModel;

    public DialogFactory(IContentDialogService contentDialogService, LaunchOptionsDialogViewModel launchOptionsDialogViewModel)
    {
        this.contentDialogService = contentDialogService;
        this.launchOptionsDialogViewModel = launchOptionsDialogViewModel;
    }
    
    public LaunchOptionsDialog CreateLaunchOptionsDialog()
    {
        return new LaunchOptionsDialog(contentDialogService, launchOptionsDialogViewModel);
    }
}
