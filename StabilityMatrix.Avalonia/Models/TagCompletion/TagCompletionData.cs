using System.Globalization;
using StabilityMatrix.Avalonia.Controls.CodeCompletion;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.Models.TagCompletion;

public class TagCompletionData : CompletionData
{
    protected TagType TagType { get; }

    /// <inheritdoc />
    public TagCompletionData(string text, TagType tagType, int? count = null)
        : base(text)
    {
        TagType = tagType;
        Icon = CompletionIcons.GetIconForTagType(tagType) ?? CompletionIcons.Invalid;

        var typeText = tagType.GetStringValue();
        Description = count is > 0 ? $"{FormatCount(count.Value)} · {typeText}" : typeText;
    }

    private static string FormatCount(int count) =>
        count switch
        {
            >= 10_000_000 => $"{count / 1_000_000}M",
            >= 1_000_000 => (count / 1_000_000.0).ToString("F1", CultureInfo.InvariantCulture) + "M",
            >= 10_000 => $"{count / 1_000}K",
            >= 1_000 => (count / 1_000.0).ToString("F1", CultureInfo.InvariantCulture) + "K",
            _ => count.ToString(CultureInfo.InvariantCulture),
        };
}
