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
            Name = "",
            FileName = "",
        }
    };
}
