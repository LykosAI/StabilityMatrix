using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentIcons.Common;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Models.Inference;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// A single queued inference generation request. Stored as a project-state snapshot
/// (<see cref="Project"/>) rather than a baked node graph so it can be reopened in a tab and edited.
/// </summary>
public partial class PromptQueueItem : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter<InferenceProjectType>))]
    public InferenceProjectType ProjectType { get; set; }

    /// <summary>
    /// Snapshot of the source tab's full state, used both to replay the generation headlessly
    /// and to reopen the item in a real tab for editing.
    /// </summary>
    public InferenceProjectDocument Project { get; set; } = new();

    [ObservableProperty]
    [JsonConverter(typeof(JsonStringEnumConverter<PromptQueueItemStatus>))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyPropertyChangedFor(nameof(IsFailed))]
    [NotifyPropertyChangedFor(nameof(IsRequeueable))]
    public partial PromptQueueItemStatus Status { get; set; } = PromptQueueItemStatus.Pending;

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial string? ProgressText { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>
    /// Single-line positive prompt text extracted from the snapshot at enqueue time, for card
    /// display. Null for tabs without a prompt card (e.g. Upscale).
    /// </summary>
    public string? PromptSnippet { get; set; }

    /// <summary>
    /// Compact generation parameters extracted from the snapshot at enqueue time,
    /// e.g. "1024×1024 · 20 steps · seed 123".
    /// </summary>
    public string? ParamsSummary { get; set; }

    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;

    [ObservableProperty]
    public partial DateTimeOffset? StartedAt { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset? CompletedAt { get; set; }

    public List<string> OutputImagePaths { get; set; } = [];

    /// <summary>
    /// Runtime-only live preview bitmap shown while the item is running. Never serialized.
    /// </summary>
    [ObservableProperty]
    [JsonIgnore]
    public partial Bitmap? PreviewImage { get; set; }

    [JsonIgnore]
    public bool IsRunning => Status == PromptQueueItemStatus.Running;

    [JsonIgnore]
    public bool IsFailed => Status == PromptQueueItemStatus.Failed;

    [JsonIgnore]
    public bool IsRequeueable =>
        Status
            is PromptQueueItemStatus.Completed
                or PromptQueueItemStatus.Failed
                or PromptQueueItemStatus.Cancelled;

    [JsonIgnore]
    public Symbol Icon => ProjectType.GetInfo()?.Icon ?? Symbol.Image;

    [JsonIgnore]
    public string StatusText =>
        Status switch
        {
            PromptQueueItemStatus.Pending => Resources.Label_StatusPending,
            PromptQueueItemStatus.Running => Resources.Label_StatusRunning,
            PromptQueueItemStatus.Completed => Resources.Label_StatusCompleted,
            PromptQueueItemStatus.Failed => Resources.Label_StatusFailed,
            PromptQueueItemStatus.Cancelled => Resources.Label_StatusCancelled,
            _ => string.Empty,
        };
}
