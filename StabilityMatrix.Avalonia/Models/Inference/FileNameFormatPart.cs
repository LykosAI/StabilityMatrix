using System;
using System.Runtime.InteropServices;
using CSharpDiscriminatedUnion.Attributes;

namespace StabilityMatrix.Avalonia.Models.Inference;

[GenerateDiscriminatedUnion(CaseFactoryPrefix = "From")]
[StructLayout(LayoutKind.Auto)]
public readonly partial struct FileNameFormatPart
{
    [StructCase("Constant", isDefaultValue: true)]
    private readonly string constant;

    [StructCase("Substitution")]
    private readonly Func<string?> substitution;
}
