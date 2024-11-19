using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.OpenModelsDb;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(OpenModelDbModelDetailsDialog))]
[ManagedService]
[Transient]
public partial class OpenModelDbModelDetailsViewModel(
    OpenModelDbManager openModelDbManager,
    IModelIndexService modelIndexService,
    IModelImportService modelImportService,
    INotificationService notificationService,
    ISettingsManager settingsManager
) : ContentDialogViewModelBase
{
    public class ModelResourceViewModel(IModelIndexService modelIndexService)
    {
        public required OpenModelDbResource Resource { get; init; }

        public string DisplayName => $"{Resource.Platform} (.{Resource.Type} file)";

        // todo: idk
        public bool IsInstalled => false;
    }

    [Required]
    public OpenModelDbKeyedModel? Model { get; set; }

    public IEnumerable<Uri> ImageUris => Model?.Images?.SelectImageAbsoluteUris() ?? [];

    public IEnumerable<ModelResourceViewModel> Resources =>
        Model
            ?.Resources
            ?.Select(resource => new ModelResourceViewModel(modelIndexService) { Resource = resource }) ?? [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    private ModelResourceViewModel? selectedResource;

    public bool CanImport => SelectedResource is { IsInstalled: false };

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
        );

        await modelImportService.DoOpenModelDbImport(Model, resourceVm.Resource, downloadFolder);

        OnPrimaryButtonClick();
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
}
