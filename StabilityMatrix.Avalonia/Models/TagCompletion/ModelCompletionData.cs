using System.IO;
using StabilityMatrix.Avalonia.Controls.CodeCompletion;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Tokens;

namespace StabilityMatrix.Avalonia.Models.TagCompletion;

public class ModelCompletionData : CompletionData
{
    protected PromptExtraNetworkType NetworkType { get; }

    /// <inheritdoc />
    public ModelCompletionData(string text, PromptExtraNetworkType networkType)
        : base(text)
    {
        NetworkType = networkType;
        // TODO: multi icons?
        Icon = CompletionIcons.Model;
        Description = networkType.GetStringValue();
    }

    public static ModelCompletionData FromLocalModel(
        LocalModelFile localModel,
        PromptExtraNetworkType networkType
    )
    {
        var displayName = Path.GetFileNameWithoutExtension(localModel.FileName);
        return new ModelCompletionData(displayName, networkType)
        {
            ImageTitle = localModel.ConnectedModelInfo?.ModelName,
            ImageSubtitle = localModel.ConnectedModelInfo?.VersionName,
            ImageSource = localModel.PreviewImageFullPathGlobal is { } img
                ? new ImageSource(new FilePath(img))
                : null
        };
    }
}
