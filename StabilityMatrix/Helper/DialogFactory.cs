using System;
using StabilityMatrix.Models;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;

namespace StabilityMatrix.Helper;

public class DialogFactory : IDialogFactory
{
    private readonly IContentDialogService contentDialogService;
    private readonly LaunchOptionsDialogViewModel launchOptionsDialogViewModel;
    private readonly LaunchViewModel launchViewModel;
    private readonly IPackageFactory packageFactory;

    public DialogFactory(IContentDialogService contentDialogService, LaunchOptionsDialogViewModel launchOptionsDialogViewModel)
    {
        this.contentDialogService = contentDialogService;
        this.launchOptionsDialogViewModel = launchOptionsDialogViewModel;
    }
    
    public LaunchOptionsDialog CreateLaunchOptionsDialog(BasePackage selectedPackage)
    {
        launchOptionsDialogViewModel.SelectedPackage = selectedPackage;
        
        return new LaunchOptionsDialog(contentDialogService, launchOptionsDialogViewModel);
    }
}
