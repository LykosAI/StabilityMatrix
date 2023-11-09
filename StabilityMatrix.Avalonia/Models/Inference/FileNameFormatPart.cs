using System;
using OneOf;

namespace StabilityMatrix.Avalonia.Models.Inference;

[GenerateOneOf]
public partial class FileNameFormatPart : OneOfBase<string, Func<string?>> { }
