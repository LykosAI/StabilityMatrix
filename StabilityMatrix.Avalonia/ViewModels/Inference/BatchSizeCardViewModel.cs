using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(BatchSizeCard))]
public partial class BatchSizeCardViewModel : LoadableViewModelBase
{
    [ObservableProperty] private int batchSize = 1;
    
    [ObservableProperty] private int batchCount = 1;
}
