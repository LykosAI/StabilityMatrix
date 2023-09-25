using StabilityMatrix.Avalonia.Controls.CodeCompletion;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.Models.TagCompletion;

public class TagCompletionData : CompletionData
{
    protected TagType TagType { get; }
    
    /// <inheritdoc />
    public TagCompletionData(string text, TagType tagType) : base(text)
    {
        TagType = tagType;
        Icon = CompletionIcons.GetIconForTagType(tagType) ?? CompletionIcons.Invalid;
        Description = tagType.GetStringValue();
    }
}
