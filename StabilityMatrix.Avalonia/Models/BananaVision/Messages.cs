using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Avalonia.Models.BananaVision;

public abstract class MessageBase(bool isMyMessage)
{
    public bool IsMyMessage { get; } = isMyMessage;
    public string Time { get; } = DateTime.Now.ToString("HH:mm");
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
    /// Toggle the expanded state
    /// </summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }
}
