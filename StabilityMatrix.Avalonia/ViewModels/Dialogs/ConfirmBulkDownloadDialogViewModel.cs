using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia.Controls.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Binding;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(ConfirmBulkDownloadDialog))]
[ManagedService]
[RegisterTransient<ConfirmBulkDownloadDialogViewModel>]
public partial class ConfirmBulkDownloadDialogViewModel(
    IModelIndexService modelIndexService,
    ISettingsManager settingsManager,
    IServiceManager<ViewModelBase> vmFactory
) : ContentDialogViewModelBase
{
    public required CivitModel Model { get; set; }

    [ObservableProperty]
    public partial double TotalSizeKb { get; set; }

    [ObservableProperty]
    public partial CivitModelFpType FpTypePreference { get; set; } = CivitModelFpType.fp16;

    [ObservableProperty]
    public partial bool IncludeVae { get; set; }

    [ObservableProperty]
    public partial string DownloadFollowingFilesText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool PreferPruned { get; set; } = true;

    private readonly SourceCache<CivitFileDisplayViewModel, int> allFilesCache = new(displayVm =>
        displayVm.FileViewModel.CivitFile.Id
    );

    public IObservableCollection<CivitFileDisplayViewModel> FilesToDownload { get; } =
        new ObservableCollectionExtended<CivitFileDisplayViewModel>();

    public ObservableCollection<CivitModelFpType> AvailableFpTypes => new(Enum.GetValues<CivitModelFpType>());

    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        if (Model.ModelVersions == null || Model.ModelVersions.Count == 0)
        {
            FilesToDownload.Clear();
            DownloadFollowingFilesText = "No files available for download.";
            TotalSizeKb = 0;
            allFilesCache.Clear(); // Clear cache if model is empty
            return;
        }

        var allFilesFromModel = Model
            .ModelVersions.SelectMany(v =>
                v.Files?.Select(f => new CivitFileDisplayViewModel
                {
                    ModelVersion = v,
                    FileViewModel = new CivitFileViewModel(
                        modelIndexService,
                        settingsManager,
                        f,
                        vmFactory,
                        null
                    )
                    {
                        InstallLocations = [],
                    },
                }) ?? []
            )
            .ToList();

        allFilesCache.Edit(updater =>
        {
            updater.Clear();
            updater.AddOrUpdate(allFilesFromModel);
        });

        var fpPreferenceObservable = this.WhenPropertyChanged(x => x.FpTypePreference)
            .Select(_ =>
                (Func<CivitFileDisplayViewModel, bool>)(
                    displayVm => IsPreferredPrecision(displayVm.FileViewModel)
                )
            );

        var includeVaeObservable = this.WhenPropertyChanged(x => x.IncludeVae)
            .Select(include =>
                (Func<CivitFileDisplayViewModel, bool>)(
                    displayVm => include.Value || displayVm.FileViewModel.CivitFile.Type != CivitFileType.VAE
                )
            );

        var preferPrunedFilter = this.WhenPropertyChanged(x => x.PreferPruned)
            .Select(_ =>
                (Func<CivitFileDisplayViewModel, bool>)(
                    displayVm =>
                    {
                        var file = displayVm.FileViewModel.CivitFile;
                        if (file.Metadata.Size is null)
                            return true;

                        if (
                            PreferPruned
                            && file.Metadata.Size.Equals("pruned", StringComparison.OrdinalIgnoreCase)
                        )
                            return true;

                        if (
                            !PreferPruned
                            && file.Metadata.Size.Equals("full", StringComparison.OrdinalIgnoreCase)
                        )
                            return true;

                        return false;
                    }
                )
            );

        var defaultFilter =
            (Func<CivitFileDisplayViewModel, bool>)(
                displayVm =>
                {
                    var fileVm = displayVm.FileViewModel;
                    if (fileVm.IsInstalled)
                        return false;

                    return fileVm.CivitFile.Type
                        is CivitFileType.Model
                            or CivitFileType.VAE
                            or CivitFileType.PrunedModel;
                }
            );

        var filteredFilesObservable = allFilesCache
            .Connect()
            .Filter(defaultFilter)
            .Filter(fpPreferenceObservable)
            .Filter(includeVaeObservable)
            .Filter(preferPrunedFilter);

        AddDisposable(
            filteredFilesObservable
                .SortAndBind(
                    FilesToDownload,
                    SortExpressionComparer<CivitFileDisplayViewModel>.Ascending(s =>
                        s.FileViewModel.CivitFile.DisplayName
                    )
                )
                .ObserveOn(SynchronizationContext.Current!)
                .Subscribe()
        );

        AddDisposable(
            filteredFilesObservable
                .ToCollection()
                .ObserveOn(SynchronizationContext.Current!) // Or AvaloniaScheduler.Instance
                .Subscribe(filteredFiles =>
                {
                    TotalSizeKb = filteredFiles.Sum(f => f.FileViewModel.CivitFile.SizeKb);
                    DownloadFollowingFilesText =
                        $"You are about to download {filteredFiles.Count} files totaling {new FileSizeType(TotalSizeKb)}.";
                })
        );

        if (
            FilesToDownload.All(x =>
                x.FileViewModel.CivitFile.Metadata.Size?.Equals("full", StringComparison.OrdinalIgnoreCase)
                ?? false
            ) && PreferPruned
        )
        {
            PreferPruned = false;
        }
    }

    public override BetterContentDialog GetDialog()
    {
        var dialog = base.GetDialog();

        dialog.MinDialogWidth = 550;
        dialog.MaxDialogHeight = 600;
        dialog.IsFooterVisible = false;
        dialog.CloseOnClickOutside = true;
        dialog.ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

        return dialog;
    }

    private bool IsPreferredPrecision(CivitFileViewModel file)
    {
        if (file.CivitFile.Metadata.Fp is null || string.IsNullOrWhiteSpace(file.CivitFile.Metadata.Fp))
            return true;

        var preference = FpTypePreference.GetStringValue();
        var fpType = file.CivitFile.Metadata.Fp;
        return preference == fpType;
    }
}
