using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.Models.HuggingFace;
using StabilityMatrix.Avalonia.ViewModels.Base;

namespace StabilityMatrix.Avalonia.ViewModels.HuggingFacePage;

public partial class HuggingfaceItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private HuggingfaceItem item;

    [ObservableProperty]
    private bool isSelected;

    public string LicenseUrl =>
        $"https://huggingface.co/{Item.RepositoryPath}/blob/main/{Item.LicensePath ?? "README.md"}";
    public string RepoUrl => $"https://huggingface.co/{Item.RepositoryPath}";

    [RelayCommand]
    private void ToggleSelected()
    {
        IsSelected = !IsSelected;
    }
}
