using System;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointManager;

public class CheckpointFileViewModel : SelectableViewModelBase
{
    public LocalModelFile CheckpointFile { get; }

    public string ThumbnailUri => CheckpointFile.PreviewImageFullPathGlobal ?? Assets.NoImage.ToString();

    public CheckpointFileViewModel(LocalModelFile checkpointFile)
    {
        CheckpointFile = checkpointFile;
    }
}
