using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Models.Tokens;

[Flags]
public enum PromptExtraNetworkType
{
    [ConvertTo<SharedFolderType>(SharedFolderType.Lora)]
    Lora = 1 << 0,

    [ConvertTo<SharedFolderType>(SharedFolderType.LyCORIS)]
    LyCORIS = 1 << 1,

    [ConvertTo<SharedFolderType>(SharedFolderType.Embeddings)]
    Embedding = 1 << 2
}
