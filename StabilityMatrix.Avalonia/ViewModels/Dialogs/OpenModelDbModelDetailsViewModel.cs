using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.OpenModelsDb;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(OpenModelDbModelDetailsDialog))]
[ManagedService]
[Transient]
public partial class OpenModelDbModelDetailsViewModel(
    OpenModelDbManager openModelDbManager,
    IModelIndexService modelIndexService
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

    public bool CanImport => SelectedResource is not null;

    [RelayCommand]
    private async Task ImportAsync(ModelResourceViewModel resourceVm) { }

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
