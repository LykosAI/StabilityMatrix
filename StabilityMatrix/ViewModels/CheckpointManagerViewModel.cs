using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Models;

namespace StabilityMatrix.ViewModels;

public partial class CheckpointManagerViewModel : ObservableObject
{
    public ObservableCollection<CheckpointFolder> CheckpointFolders { get; set; } = new();
    
    public async Task OnLoaded()
    {
        CheckpointFolders.Clear();
        var folder = new CheckpointFolder
        {
            Title = "Stable Diffusion",
            DirectoryPath = @"L:\Image ML\stable-diffusion-webui\models\Stable-diffusion"
        };
        CheckpointFolders.Add(folder);
        await folder.IndexAsync();
    }

    public void OnFolderCardDrop()
    {
        
    }
}
