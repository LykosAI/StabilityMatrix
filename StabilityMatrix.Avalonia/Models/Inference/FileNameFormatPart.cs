using System;

namespace StabilityMatrix.Avalonia.Models.Inference;

public record FileNameFormatPart(string? Constant, Func<string?>? Substitution);
