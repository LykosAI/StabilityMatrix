using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models.Api.HuggingFace;

namespace StabilityMatrix.Avalonia.ViewModels.HuggingFacePage;

/// <summary>
/// A single model card in the live HuggingFace search results. Selecting it asks the
/// parent page to load that repo's file tree.
/// </summary>
public partial class HuggingFaceSearchResultViewModel : ViewModelBase
{
    public required HuggingFaceSearchEntry Entry { get; init; }

    /// <summary>Invoked with the repo id when the user picks this result.</summary>
    public Action<string>? OnSelected { get; init; }

    public string Id => Entry.Id;

    /// <summary>The owner/org portion of the id (before the slash), or null if none.</summary>
    public string? Author => Entry.Id.Contains('/') ? Entry.Id[..Entry.Id.IndexOf('/')] : null;

    public bool HasAuthor => Author is not null;

    /// <summary>The model name portion of the id (after the slash).</summary>
    public string ModelName => Entry.Id.Contains('/') ? Entry.Id[(Entry.Id.IndexOf('/') + 1)..] : Entry.Id;

    public string RepoUrl => $"https://huggingface.co/{Entry.Id}";

    public string DownloadsText => FormatCount(Entry.Downloads);

    public string LikesText => FormatCount(Entry.Likes);

    public string? TagsText => Entry.Tags is { Count: > 0 } tags ? string.Join("  ", tags.Take(4)) : null;

    public string? PipelineTag => Entry.PipelineTag;

    public bool HasPipelineTag => !string.IsNullOrWhiteSpace(Entry.PipelineTag);

    public string? LastUpdatedText => Entry.LastModified is { } t ? $"updated {FormatRelativeTime(t)}" : null;

    [RelayCommand]
    private void Select() => OnSelected?.Invoke(Entry.Id);

    private static string FormatCount(long value) =>
        value switch
        {
            >= 1_000_000 => $"{value / 1_000_000d:0.#}M",
            >= 1_000 => $"{value / 1_000d:0.#}K",
            _ => value.ToString(),
        };

    private static string FormatRelativeTime(DateTimeOffset time)
    {
        var span = DateTimeOffset.UtcNow - time;
        return span switch
        {
            { TotalDays: >= 365 } => $"{(int)(span.TotalDays / 365)}y ago",
            { TotalDays: >= 30 } => $"{(int)(span.TotalDays / 30)}mo ago",
            { TotalDays: >= 1 } => $"{(int)span.TotalDays}d ago",
            { TotalHours: >= 1 } => $"{(int)span.TotalHours}h ago",
            { TotalMinutes: >= 1 } => $"{(int)span.TotalMinutes}m ago",
            _ => "just now",
        };
    }
}
