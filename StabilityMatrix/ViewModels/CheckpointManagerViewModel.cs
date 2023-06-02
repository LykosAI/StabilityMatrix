using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Models;

namespace StabilityMatrix.ViewModels;

public partial class CheckpointManagerViewModel : ObservableObject
{
    public ObservableCollection<CheckpointFolderCard> CheckpointFolderCards { get; set; } = new()
    {
        new()
        {
            Name = "Stable Diffusion"
        },
        new()
        {
            Name = "Lora"
        }
    };
}
