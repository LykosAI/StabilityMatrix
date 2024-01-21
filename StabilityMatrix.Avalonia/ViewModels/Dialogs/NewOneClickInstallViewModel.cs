using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[Transient]
[ManagedService]
public partial class NewOneClickInstallViewModel : ContentDialogViewModelBase
{
    public SourceCache<BasePackage, string> AllPackagesCache { get; } = new(p => p.Author + p.Name);

    public IObservableCollection<BasePackage> ShownPackages { get; set; } =
        new ObservableCollectionExtended<BasePackage>();

    [ObservableProperty]
    private bool showIncompatiblePackages;

    public NewOneClickInstallViewModel(IPackageFactory packageFactory)
    {
        var incompatiblePredicate = this.WhenPropertyChanged(vm => vm.ShowIncompatiblePackages)
            .Select(_ => new Func<BasePackage, bool>(p => p.IsCompatible || ShowIncompatiblePackages))
            .AsObservable();

        AllPackagesCache
            .Connect()
            .DeferUntilLoaded()
            .Filter(incompatiblePredicate)
            .Filter(p => p.OfferInOneClickInstaller || ShowIncompatiblePackages)
            .Sort(
                SortExpressionComparer<BasePackage>
                    .Ascending(p => p.InstallerSortOrder)
                    .ThenByAscending(p => p.DisplayName)
            )
            .Bind(ShownPackages)
            .Subscribe();

        AllPackagesCache.AddOrUpdate(packageFactory.GetAllAvailablePackages());
    }

    [RelayCommand]
    private async Task InstallComfyForInference()
    {
        var comfyPackage = ShownPackages.FirstOrDefault(x => x is ComfyUI);
        if (comfyPackage != null)
        {
            // install
        }
    }
}
