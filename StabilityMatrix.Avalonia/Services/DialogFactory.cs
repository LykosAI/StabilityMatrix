using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

public class DialogFactory : IDialogFactory
{
    private readonly ISettingsManager settingsManager;
    private readonly IDownloadService downloadService;
    private readonly IPackageFactory packageFactory;
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private readonly ILogger<OneClickInstallViewModel> logger;
    private readonly IPyRunner pyRunner;
    private readonly ISharedFolders sharedFolders;

    public DialogFactory(ISettingsManager settingsManager, IDownloadService downloadService,
        IPackageFactory packageFactory, IPrerequisiteHelper prerequisiteHelper,
        ILogger<OneClickInstallViewModel> logger, IPyRunner pyRunner, ISharedFolders sharedFolders)
    {
        this.settingsManager = settingsManager;
        this.downloadService = downloadService;
        this.packageFactory = packageFactory;
        this.prerequisiteHelper = prerequisiteHelper;
        this.logger = logger;
        this.pyRunner = pyRunner;
        this.sharedFolders = sharedFolders;
    }

    public SelectModelVersionViewModel CreateSelectModelVersionViewModel(CivitModel model,
        ContentDialog dialog) => new(model, dialog, settingsManager, downloadService);

    public OneClickInstallViewModel CreateOneClickInstallViewModel() =>
        new(settingsManager, packageFactory, prerequisiteHelper, logger, pyRunner, sharedFolders);
}
