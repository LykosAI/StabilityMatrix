using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Avalonia.Models.BananaVision;

public abstract class MessageBase(bool isMyMessage)
{
    public bool IsMyMessage { get; } = isMyMessage;
    public string Time { get; } = DateTime.Now.ToString("HH:mm");

    /// <summary>
    /// Optional database message ID backing this UI message.
    /// When set, the UI can support actions like delete.
    /// </summary>
    public Guid? DatabaseMessageId { get; init; }

    /// <summary>
    /// Optional file path for image messages (when backed by a stored image on disk).
    /// </summary>
    public string? FilePath { get; init; }

    public bool CanDelete => DatabaseMessageId.HasValue;
}

/// <summary>
/// Text message implementation
/// </summary>
public class TextMessage(string text, bool isMyMessage) : MessageBase(isMyMessage)
{
    public string Text { get; } = text;
}

/// <summary>
/// Image message implementation
/// </summary>
public class ImageMessage(Bitmap image, bool isMyMessage) : MessageBase(isMyMessage)
{
    public Bitmap? Image { get; } = image;
}

/// <summary>
/// Loading placeholder message shown while image is being generated
/// </summary>
public class LoadingImageMessage() : MessageBase(false)
{
    public int TargetWidth { get; init; } = 350;
    public int TargetHeight { get; init; } = 350;
}

/// <summary>
/// Thinking/reasoning message from Gemini 3 Pro
/// Displays as a collapsible section showing the AI's reasoning process
/// </summary>
public partial class ThinkingMessage : ObservableObject
{
    public ThinkingMessage(string thinkingContent)
    {
        ThinkingContent = thinkingContent;
        IsExpanded = false;
        Time = DateTime.Now.ToString("HH:mm");
    }

    /// <summary>
    /// The thinking/reasoning content from the model
    /// </summary>
    public string ThinkingContent { get; }

    /// <summary>
    /// Preview of the thinking content (first ~100 chars)
    /// </summary>
    public string ThinkingPreview =>
        ThinkingContent.Length > 100 ? ThinkingContent[..100] + "..." : ThinkingContent;

    /// <summary>
    /// Whether the thinking section is expanded
    /// </summary>
    [ObservableProperty]
    private bool isExpanded;

    /// <summary>
    /// Time the message was created
    /// </summary>
    public string Time { get; }

    /// <summary>
    /// Optional database message ID backing this thinking block.
    /// </summary>
    public Guid? DatabaseMessageId { get; init; }

    public bool CanDelete => DatabaseMessageId.HasValue;

    /// <summary>
    /// Toggle the expanded state
    /// </summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }
}
