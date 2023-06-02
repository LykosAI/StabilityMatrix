using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Models;

public partial class CheckpointFolderCard : ObservableObject
{
    [ObservableProperty]
    private string name;

    public ObservableCollection<CheckpointCard> CheckpointCards { get; set; } = new()
    {
        new CheckpointCard
        {
            Name = "Stable Diffusion v1.5",
            FileName = "v1-5-pruned-emaonly.safetensors",
        },
        new CheckpointCard
        {
            Name = "Stable Diffusion v1.5 (EMA)",
            FileName = "v1-5-emaonly.safetensors",
        },
        new CheckpointCard
        {
            Name = "Stable Diffusion v1.5 (EMA, 512x512)",
            FileName = "v1-5-emaonly-512.safetensors",
        },
        new CheckpointCard
        {
            Name = "Stable Diffusion v2.0",
            FileName = "v2-0-pruned-emaonly.safetensors",
        },
    };
}
