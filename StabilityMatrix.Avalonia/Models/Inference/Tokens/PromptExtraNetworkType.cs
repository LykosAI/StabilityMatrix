using System;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Models.Inference.Tokens;

[Flags]
public enum PromptExtraNetworkType
{
    [ConvertTo<SharedFolderType>(SharedFolderType.Lora)]
    Lora = 1 << 0,
    [ConvertTo<SharedFolderType>(SharedFolderType.LyCORIS)]
    LyCORIS = 1 << 1,
    [ConvertTo<SharedFolderType>(SharedFolderType.TextualInversion)]
    Embedding = 1 << 2
}
