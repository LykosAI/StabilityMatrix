using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace StabilityMatrix.Avalonia.Models.Inference;

public record FileNameFormat
{
    public string Template { get; }

    public string Prefix { get; set; } = "";

    public string Postfix { get; set; } = "";

    public IReadOnlyList<FileNameFormatPart> Parts { get; }

    private FileNameFormat(string template, IReadOnlyList<FileNameFormatPart> parts)
    {
        Template = template;
        Parts = parts;
    }

    public FileNameFormat WithBatchPostFix(int current, int total)
    {
        return this with { Postfix = Postfix + $" ({current}-{total})" };
    }

    public FileNameFormat WithGridPrefix()
    {
        return this with { Prefix = Prefix + "Grid_" };
    }

    public string GetFileName()
    {
        return Prefix
            + string.Join(
                "",
                Parts.Select(part =>
                    part.Match(
                        constant => constant,
                        substitution =>
                        {
                            // Filter invalid path chars
                            var result = substitution.Invoke();
                            return result is null
                                ? null
                                : Path.GetInvalidFileNameChars()
                                    .Aggregate(result, (current, c) => current.Replace(c, '_'));
                        }
                    )
                )
            )
            + Postfix;
    }

    public static FileNameFormat Parse(string template, FileNameFormatProvider provider)
    {
        var parts = provider.GetParts(template).ToImmutableArray();
        return new FileNameFormat(template, parts);
    }

    public static bool TryParse(
        string template,
        FileNameFormatProvider provider,
        [NotNullWhen(true)] out FileNameFormat? format
    )
    {
        try
        {
            format = Parse(template, provider);
            return true;
        }
        catch (ArgumentException)
        {
            format = null;
            return false;
        }
    }

    public const string DefaultTemplate = "{date}_{time}-{model_name}-{seed}";
    public const string DefaultModelBrowserTemplate = "{file_name}";
}
