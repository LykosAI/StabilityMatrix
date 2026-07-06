using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
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
    private string name = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter<InferenceProjectType>))]
    public InferenceProjectType ProjectType { get; set; }

    /// <summary>
    /// Snapshot of the source tab's full state, used both to replay the generation headlessly
    /// and to reopen the item in a real tab for editing.
    /// </summary>
    public InferenceProjectDocument Project { get; set; } = new();

    [ObservableProperty]
    [property: JsonConverter(typeof(JsonStringEnumConverter<PromptQueueItemStatus>))]
    private PromptQueueItemStatus status = PromptQueueItemStatus.Pending;

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private string? progressText;

    [ObservableProperty]
    private string? errorMessage;

    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;

    [ObservableProperty]
    private DateTimeOffset? startedAt;

    [ObservableProperty]
    private DateTimeOffset? completedAt;

    public List<string> OutputImagePaths { get; set; } = [];

    /// <summary>
    /// Runtime-only live preview bitmap shown while the item is running. Never serialized.
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private Bitmap? previewImage;
}
