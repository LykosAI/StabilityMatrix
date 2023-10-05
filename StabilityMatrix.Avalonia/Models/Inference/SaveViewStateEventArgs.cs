using System;
using System.Threading.Tasks;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// Event for view models requesting to get their view state for saving
/// </summary>
public class SaveViewStateEventArgs : EventArgs
{
    public Task<ViewState>? StateTask { get; set; }
}
