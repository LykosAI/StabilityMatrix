using StabilityMatrix.Avalonia.Controls.CodeCompletion;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Tokens;

namespace StabilityMatrix.Avalonia.Models.TagCompletion;

public class ModelTypeCompletionData : CompletionData
{
    protected PromptExtraNetworkType NetworkType { get; }

    /// <inheritdoc />
    public ModelTypeCompletionData(string text, PromptExtraNetworkType networkType)
        : base(text)
    {
        NetworkType = networkType;
        Icon = CompletionIcons.ModelType;
        Description = $"{networkType.GetStringValue()} Network";
    }
}
