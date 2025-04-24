using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.Models.HuggingFace;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;

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

    public required string? ModelsDir { get; init; }

    public bool Exists =>
        ModelsDir != null
        && File.Exists(
            Path.Combine(
                ModelsDir,
                Item.ModelCategory.ConvertTo<SharedFolderType>().ToString(),
                Item.Subfolder ?? string.Empty,
                Path.GetFileName(Item.Files[0]).Equals("ae.safetensors")
                && Item.ModelName.Equals("HiDream I1 VAE")
                    ? "hidream_vae.safetensors"
                    : Path.GetFileName(Item.Files[0])
            )
        );

    [RelayCommand]
    private void ToggleSelected()
    {
        IsSelected = !IsSelected;
    }

    public void NotifyExistsChanged()
    {
        OnPropertyChanged(nameof(Exists));
    }
}
