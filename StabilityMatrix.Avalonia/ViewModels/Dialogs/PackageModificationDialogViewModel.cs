using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public class PackageModificationDialogViewModel : ContentDialogProgressViewModelBase
{
    private readonly IPackageModificationRunner packageModificationRunner;
    private readonly INotificationService notificationService;
    private readonly IEnumerable<IPackageStep> steps;

    public PackageModificationDialogViewModel(IPackageModificationRunner packageModificationRunner,
        INotificationService notificationService, IEnumerable<IPackageStep> steps)
    {
        this.packageModificationRunner = packageModificationRunner;
        this.notificationService = notificationService;
        this.steps = steps;
    }
    
    public ConsoleViewModel Console { get; } = new();

    public override async Task OnLoadedAsync()
    {
        // idk why this is getting called twice
        if (!packageModificationRunner.IsRunning)
        {
            packageModificationRunner.ProgressChanged += PackageModificationRunnerOnProgressChanged;
            await packageModificationRunner.ExecuteSteps(steps.ToList());

            notificationService.Show("Package Install Completed",
                "Package install completed successfully.", NotificationType.Success);
            
            OnCloseButtonClick();
        }
    }

    private void PackageModificationRunnerOnProgressChanged(object? sender, ProgressReport e)
    {
        Text = string.IsNullOrWhiteSpace(e.Title)
            ? packageModificationRunner.CurrentStep?.ProgressTitle
            : e.Title;
        
        Value = e.Percentage;
        Description = e.Message;
        IsIndeterminate = e.IsIndeterminate;

        if (string.IsNullOrWhiteSpace(e.Message) || e.Message.Equals("Downloading...")) 
            return;
        
        Console.PostLine(e.Message);
        EventManager.Instance.OnScrollToBottomRequested();
    }
}
