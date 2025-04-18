using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(BatchSizeCard))]
[ManagedService]
[RegisterScoped<BatchSizeCardViewModel>]
public partial class BatchSizeCardViewModel : LoadableViewModelBase, IComfyStep
{
    [NotifyDataErrorInfo]
    [ObservableProperty]
    [Range(1, 1024)]
    private int batchSize = 1;

    [NotifyDataErrorInfo]
    [ObservableProperty]
    [Range(1, int.MaxValue)]
    private int batchCount = 1;

    [NotifyDataErrorInfo]
    [ObservableProperty]
    [Required]
    private bool isBatchIndexEnabled;

    [NotifyDataErrorInfo]
    [ObservableProperty]
    [Range(1, 1024)]
    private int batchIndex = 1;

    /// <summary>
    /// Sets batch size to connections.
    /// Provides:
    /// <list type="number">
    /// <item><see cref="ComfyNodeBuilder.NodeBuilderConnections.BatchSize"/></item>
    /// <item><see cref="ComfyNodeBuilder.NodeBuilderConnections.BatchIndex"/></item>
    /// </list>
    /// </summary>
    public void ApplyStep(ModuleApplyStepEventArgs e)
    {
        e.Builder.Connections.BatchSize = BatchSize;
        e.Builder.Connections.BatchIndex = IsBatchIndexEnabled ? BatchIndex : null;
    }
}
