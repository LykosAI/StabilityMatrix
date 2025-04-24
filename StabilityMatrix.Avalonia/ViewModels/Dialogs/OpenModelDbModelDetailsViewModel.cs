using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.OpenModelsDb;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(OpenModelDbModelDetailsDialog))]
[ManagedService]
[RegisterTransient<OpenModelDbModelDetailsViewModel>]
public partial class OpenModelDbModelDetailsViewModel(
    OpenModelDbManager openModelDbManager,
    IModelIndexService modelIndexService,
    IModelImportService modelImportService,
    INotificationService notificationService,
    ISettingsManager settingsManager,
    IServiceManager<ViewModelBase> dialogFactory
) : ContentDialogViewModelBase
{
    public class ModelResourceViewModel(IModelIndexService modelIndexService)
    {
        public required OpenModelDbResource Resource { get; init; }

        public string DisplayName => $"{Resource.Platform} (.{Resource.Type} file)";

        public bool IsInstalled =>
            modelIndexService.FindBySha256Async(Resource.Sha256).GetAwaiter().GetResult().Any();
    }

    [Required]
    public OpenModelDbKeyedModel? Model { get; set; }

    public IEnumerable<Uri> ImageUris =>
        Model?.Images?.SelectImageAbsoluteUris().Any() ?? false
            ? Model?.Images?.SelectImageAbsoluteUris() ?? [Assets.NoImage]
            : [Assets.NoImage];

    public IEnumerable<ModelResourceViewModel> Resources =>
        Model
            ?.Resources
            ?.Select(resource => new ModelResourceViewModel(modelIndexService) { Resource = resource }) ?? [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    private ModelResourceViewModel? selectedResource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomSelected))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyPathWarning))]
    private string selectedInstallLocation = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> availableInstallLocations = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyPathWarning))]
    private string customInstallLocation = string.Empty;

    public bool IsCustomSelected => SelectedInstallLocation == "Custom...";
    public bool ShowEmptyPathWarning => IsCustomSelected && string.IsNullOrWhiteSpace(CustomInstallLocation);
    public bool CanImport => SelectedResource is { IsInstalled: false };

    public override void OnLoaded()
    {
        if (Design.IsDesignMode || !settingsManager.IsLibraryDirSet)
            return;

        LoadInstallLocations();
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportAsync(ModelResourceViewModel? resourceVm)
    {
        if (resourceVm?.Resource is null || Model is null)
            return;

        if (
            Model.GetSharedFolderType() is not { } sharedFolderType
            || sharedFolderType is SharedFolderType.Unknown
        )
        {
            notificationService.ShowPersistent(
                "Failed to import model",
                $"Model Architecture '{Model.Architecture}' not supported",
                NotificationType.Error
            );
            return;
        }

        var downloadFolder = new DirectoryPath(
            settingsManager.ModelsDirectory,
            sharedFolderType.GetStringValue()
        ).ToString();

        var useCustomLocation =
            !string.IsNullOrWhiteSpace(CustomInstallLocation) && Directory.Exists(CustomInstallLocation);

        if (useCustomLocation)
        {
            var customFolder = new DirectoryPath(CustomInstallLocation);
            customFolder.Create();

            downloadFolder = customFolder.ToString();
        }

        if (!string.IsNullOrWhiteSpace(SelectedInstallLocation) && SelectedInstallLocation != "Custom...")
        {
            var selectedFolder = SelectedInstallLocation
                .Replace("Models", string.Empty)
                .Replace(Path.DirectorySeparatorChar.ToString(), string.Empty);
            downloadFolder = new DirectoryPath(settingsManager.ModelsDirectory, selectedFolder);
        }

        await modelImportService.DoOpenModelDbImport(
            Model,
            resourceVm.Resource,
            downloadFolder,
            download => download.ContextAction = new ModelPostDownloadContextAction()
        );

        OnPrimaryButtonClick();
    }

    [RelayCommand]
    private async Task DeleteModel(ModelResourceViewModel? resourceVm)
    {
        if (SelectedResource == null)
            return;

        var fileToDelete = SelectedResource;

        var hash = fileToDelete.Resource.Sha256;
        if (string.IsNullOrWhiteSpace(hash))
        {
            notificationService.Show(
                "Error deleting file",
                "Could not delete model, hash is missing.",
                NotificationType.Error
            );
            return;
        }

        var matchingModels = (await modelIndexService.FindBySha256Async(hash)).ToList();

        if (matchingModels.Count == 0)
        {
            await modelIndexService.RefreshIndex();
            matchingModels = (await modelIndexService.FindBySha256Async(hash)).ToList();

            if (matchingModels.Count == 0)
            {
                notificationService.Show(
                    "Error deleting file",
                    "Could not delete model, model not found in index.",
                    NotificationType.Error
                );
                return;
            }
        }

        var confirmDeleteVm = dialogFactory.Get<ConfirmDeleteDialogViewModel>();
        var pathsToDelete = new List<string>();
        foreach (var localModel in matchingModels)
        {
            var checkpointPath = new FilePath(localModel.GetFullPath(settingsManager.ModelsDirectory));
            var previewPath = localModel.GetPreviewImageFullPath(settingsManager.ModelsDirectory);
            var cmInfoPath = checkpointPath.ToString().Replace(checkpointPath.Extension, ".cm-info.json");

            pathsToDelete.Add(checkpointPath);
            pathsToDelete.Add(previewPath);
            pathsToDelete.Add(cmInfoPath);
        }

        confirmDeleteVm.PathsToDelete = pathsToDelete;

        if (await confirmDeleteVm.GetDialog().ShowAsync() != ContentDialogResult.Primary)
            return;

        try
        {
            await confirmDeleteVm.ExecuteCurrentDeleteOperationAsync(failFast: true);
        }
        catch (Exception e)
        {
            notificationService.ShowPersistent("Error deleting folder", e.Message, NotificationType.Error);
            return;
        }
    }

    partial void OnSelectedInstallLocationChanged(string? value)
    {
        if (value?.Equals("Custom...", StringComparison.OrdinalIgnoreCase) is true)
        {
            Dispatcher.UIThread.InvokeAsync(SelectCustomFolder);
        }
        else
        {
            CustomInstallLocation = string.Empty;
        }
    }

    public override BetterContentDialog GetDialog()
    {
        var dialog = base.GetDialog();
        dialog.IsFooterVisible = false;
        dialog.CloseOnClickOutside = true;
        dialog.FullSizeDesired = true;
        dialog.ContentMargin = new Thickness(8);
        return dialog;
    }

    public async Task SelectCustomFolder()
    {
        var files = await App.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Select Download Folder",
                AllowMultiple = false,
                SuggestedStartLocation = await App.StorageProvider.TryGetFolderFromPathAsync(
                    Path.Combine(
                        settingsManager.ModelsDirectory,
                        Model.GetSharedFolderType().GetStringValue()
                    )
                )
            }
        );

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
        {
            CustomInstallLocation = path;
        }
    }

    private void LoadInstallLocations()
    {
        var installLocations = new ObservableCollection<string>();

        var rootModelsDirectory = new DirectoryPath(settingsManager.ModelsDirectory);

        var downloadDirectory = new DirectoryPath(
            rootModelsDirectory,
            Model.GetSharedFolderType().GetStringValue()
        );

        if (!downloadDirectory.ToString().EndsWith("Unknown"))
        {
            installLocations.Add(downloadDirectory.ToString().Replace(rootModelsDirectory, "Models"));
            foreach (
                var directory in downloadDirectory.EnumerateDirectories(
                    "*",
                    EnumerationOptionConstants.AllDirectories
                )
            )
            {
                installLocations.Add(directory.ToString().Replace(rootModelsDirectory, "Models"));
            }
        }

        installLocations.Add("Custom...");

        AvailableInstallLocations = installLocations;
        SelectedInstallLocation = installLocations.FirstOrDefault();
    }
}
