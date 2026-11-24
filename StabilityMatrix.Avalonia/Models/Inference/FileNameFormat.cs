using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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

    /// <summary>
    /// Resolves variable values once, retaining the prefix and postfix for subsequent filename writes.
    /// </summary>
    public FileNameFormat WithResolvedVariables()
    {
        var name = GetFileName();
        return new FileNameFormat(Template, [name[Prefix.Length..(name.Length - Postfix.Length)]])
        {
            Prefix = Prefix,
            Postfix = Postfix,
        };
    }

    /// <summary>
    /// Formats an output path with its extension and collision suffix, limiting the final filename
    /// component to 255 UTF-8 bytes. Long stems retain a deterministic hash of the full stem;
    /// batch and collision suffixes and the extension are preserved.
    /// </summary>
    public string GetOutputFileName(string fileExtension, string collisionSuffix = "")
    {
        // The UTF-8 limit also fits Windows' 255 UTF-16-unit filename limit.
        const int maxFileNameBytes = 255;
        var formattedName = GetFileName();
        var pathStem = formattedName[..(formattedName.Length - Postfix.Length)];
        var stem = Path.GetFileName(pathStem);
        var directory = pathStem[..(pathStem.Length - stem.Length)];
        var suffix = Postfix + collisionSuffix + "." + fileExtension;

        if (Encoding.UTF8.GetByteCount(stem) + Encoding.UTF8.GetByteCount(suffix) <= maxFileNameBytes)
        {
            return directory + stem + suffix;
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(stem)))[..16];
        var shortenedSuffix = "_" + hash + suffix;
        var stemBudget = maxFileNameBytes - Encoding.UTF8.GetByteCount(shortenedSuffix);
        if (stemBudget < 0)
        {
            throw new ArgumentException(
                "Output filename suffix exceeds the filename limit",
                nameof(fileExtension)
            );
        }

        var prefix = new StringBuilder();
        var prefixBytes = 0;
        foreach (var rune in stem.EnumerateRunes())
        {
            if (prefixBytes + rune.Utf8SequenceLength > stemBudget)
            {
                break;
            }

            prefix.Append(rune.ToString());
            prefixBytes += rune.Utf8SequenceLength;
        }

        return directory + prefix.ToString() + shortenedSuffix;
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

    /// <summary>
    /// Starting pattern for Organize Models when the user has not saved one: sorts into
    /// base-model folders so the first-run preview proposes something visible. Also offered
    /// as the one-click suggestion when a saved pattern would move nothing.
    /// </summary>
    public const string DefaultOrganizationTemplate = "{base_model}/{file_name}";
}
