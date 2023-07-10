using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

public class DialogFactory : IDialogFactory
{
    private readonly ISettingsManager settingsManager;
    private readonly IDownloadService downloadService;

    public DialogFactory(ISettingsManager settingsManager, IDownloadService downloadService)
    {
        this.settingsManager = settingsManager;
        this.downloadService = downloadService;
    }
    
    public SelectModelVersionViewModel CreateSelectModelVersionViewModel(CivitModel model, ContentDialog dialog)
    {
        return new SelectModelVersionViewModel(model, dialog, settingsManager, downloadService);
    }
}
