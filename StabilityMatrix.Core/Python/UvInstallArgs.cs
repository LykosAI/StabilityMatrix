using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Text.RegularExpressions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Python;

/// <summary>
/// Builds arguments for 'uv pip install' commands.
/// </summary>
[SuppressMessage("ReSharper", "StringLiteralTypo")]
public record UvInstallArgs : ProcessArgsBuilder
{
    public UvInstallArgs(params Argument[] arguments)
        : base(arguments) { }

    /// <summary>
    /// Adds the Torch package.
    /// </summary>
    /// <param name="versionSpecifier">Optional version specifier (e.g., "==2.1.0+cu118", ">=2.0").</param>
    public UvInstallArgs WithTorch(string versionSpecifier = "") =>
        this.AddArg(UvPackageSpecifier.Parse($"torch{versionSpecifier}"));

    /// <summary>
    /// Adds the Torch-DirectML package.
    /// </summary>
    /// <param name="versionSpecifier">Optional version specifier.</param>
    public UvInstallArgs WithTorchDirectML(string versionSpecifier = "") =>
        this.AddArg(UvPackageSpecifier.Parse($"torch-directml{versionSpecifier}"));

    /// <summary>
    /// Adds the TorchVision package.
    /// </summary>
    /// <param name="versionSpecifier">Optional version specifier.</param>
    public UvInstallArgs WithTorchVision(string versionSpecifier = "") =>
        this.AddArg(UvPackageSpecifier.Parse($"torchvision{versionSpecifier}"));

    /// <summary>
    /// Adds the TorchAudio package.
    /// </summary>
    /// <param name="versionSpecifier">Optional version specifier.</param>
    public UvInstallArgs WithTorchAudio(string versionSpecifier = "") =>
        this.AddArg(UvPackageSpecifier.Parse($"torchaudio{versionSpecifier}"));

    /// <summary>
    /// Adds the xFormers package.
    /// </summary>
    /// <param name="versionSpecifier">Optional version specifier.</param>
    public UvInstallArgs WithXFormers(string versionSpecifier = "") =>
        this.AddArg(UvPackageSpecifier.Parse($"xformers{versionSpecifier}"));

    /// <summary>
    /// Adds an extra index URL.
    /// uv equivalent: --extra-index-url &lt;URL&gt;
    /// </summary>
    /// <param name="indexUrl">The URL of the extra index.</param>
    public UvInstallArgs WithExtraIndex(string indexUrl) =>
        this.AddKeyedArgs("--extra-index-url", ["--extra-index-url", indexUrl]);

    /// <summary>
    /// Adds the PyTorch specific extra index URL.
    /// </summary>
    /// <param name="torchIndexVariant">The PyTorch index variant (e.g., "cu118", "cu121", "cpu").</param>
    public UvInstallArgs WithTorchExtraIndex(string torchIndexVariant) =>
        WithExtraIndex($"https://download.pytorch.org/whl/{torchIndexVariant}");

    /// <summary>
    /// Parses package specifiers from a requirements.txt-formatted string.
    /// Lines starting with '#' are ignored. Inline comments are removed.
    /// </summary>
    /// <param name="requirements">The string content of a requirements.txt file.</param>
    /// <param name="excludePattern">Optional regex pattern to exclude packages by name.</param>
    public UvInstallArgs WithParsedFromRequirementsTxt(
        string requirements,
        [StringSyntax(StringSyntaxAttribute.Regex)] string? excludePattern = null
    )
    {
        var lines = requirements
            .SplitLines(StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(s => !s.StartsWith('#'))
            .Select(s => s.Contains('#') ? s.Substring(0, s.IndexOf('#')).Trim() : s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s));

        var argumentsToAdd = new List<Argument>();
        Regex? excludeRegex = null;
        if (excludePattern is not null)
        {
            excludeRegex = new Regex($"^{excludePattern}$", RegexOptions.Compiled);
        }

        foreach (var line in lines)
        {
            try
            {
                var specifier = UvPackageSpecifier.Parse(line);
                if (
                    excludeRegex is not null
                    && specifier.Name is not null
                    && excludeRegex.IsMatch(specifier.Name)
                )
                {
                    continue;
                }
                argumentsToAdd.Add(specifier); // Implicit conversion to Argument
            }
            catch (ArgumentException ex)
            {
                // Line is not a valid UvPackageSpecifier according to UvPackageSpecifier.Parse.
                // This could be a pip command/option (e.g., flags like --no-cache-dir, -r other.txt, -e path).
                // If the line starts with a hyphen, treat it as a command-line option directly.
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("-"))
                {
                    // Add as a raw argument. ProcessArgsBuilder usually handles splitting if it's like "--key value".
                    // Or it could be a simple flag like "--no-deps".
                    argumentsToAdd.Add(new Argument(trimmedLine));
                }
                else
                {
                    // Log or handle other unparseable lines if necessary. For now, skipping non-flag unparseable lines.
                    // Logger.Warn($"Skipping unparseable line in requirements: {line}. Exception: {ex.Message}");
                }
            }
        }

        return this.AddArgs(argumentsToAdd.ToArray());
    }

    /// <summary>
    /// Applies user-defined overrides to the package specifiers.
    /// </summary>
    /// <param name="overrides">A list of package specifier overrides.</param>
    public UvInstallArgs WithUserOverrides(List<UvPackageSpecifierOverride> overrides)
    {
        var newArgs = this;

        foreach (var uvOverride in overrides)
        {
            if (string.IsNullOrWhiteSpace(uvOverride.Name))
                continue;

            // Special handling for index URLs, ensuring constraint is treated as assignment
            if (uvOverride.Name is "--extra-index-url" or "--index-url")
            {
                uvOverride.Constraint = "="; // Or ensure ToArgument() for these produces correct format.
            }

            var uvOverrideArg = uvOverride.ToArgument();

            if (uvOverride.Action is UvPackageSpecifierOverrideAction.Update)
            {
                newArgs = newArgs.RemoveUvArgKey(uvOverrideArg.Key ?? uvOverrideArg.Value);
                newArgs = newArgs.AddArg(uvOverrideArg);
            }
            else if (uvOverride.Action is UvPackageSpecifierOverrideAction.Remove)
            {
                newArgs = newArgs.RemoveUvArgKey(uvOverrideArg.Key ?? uvOverrideArg.Value);
            }
        }
        return newArgs;
    }

    public UvInstallArgs WithUserOverrides(List<PipPackageSpecifierOverride> overrides)
    {
        var newArgs = this;

        foreach (var uvOverride in overrides)
        {
            if (string.IsNullOrWhiteSpace(uvOverride.Name))
                continue;

            // Special handling for index URLs, ensuring constraint is treated as assignment
            if (uvOverride.Name is "--extra-index-url" or "--index-url")
            {
                uvOverride.Constraint = "=";
            }

            var uvOverrideArg = uvOverride.ToArgument();

            if (uvOverride.Action is PipPackageSpecifierOverrideAction.Update)
            {
                newArgs = newArgs.RemoveUvArgKey(uvOverrideArg.Key ?? uvOverrideArg.Value);
                newArgs = newArgs.AddArg(uvOverrideArg);
            }
            else if (uvOverride.Action is PipPackageSpecifierOverrideAction.Remove)
            {
                newArgs = newArgs.RemoveUvArgKey(uvOverrideArg.Key ?? uvOverrideArg.Value);
            }
        }
        return newArgs;
    }

    /// <summary>
    /// Removes an argument or package specifier by its key.
    /// For packages, the key is typically the package name.
    /// </summary>
    [Pure]
    public UvInstallArgs RemoveUvArgKey(string argumentKey)
    {
        return this with
        {
            Arguments = Arguments
                .Where(
                    arg =>
                        arg.HasKey
                            ? (arg.Key != argumentKey)
                            : (
                                arg.Value != argumentKey
                                && !(
                                    arg.Value.StartsWith($"{argumentKey}==")
                                    || arg.Value.StartsWith($"{argumentKey}~=")
                                    || arg.Value.StartsWith($"{argumentKey}>=")
                                    || arg.Value.StartsWith($"{argumentKey}<=")
                                    || arg.Value.StartsWith($"{argumentKey}!=")
                                    || arg.Value.StartsWith($"{argumentKey}>")
                                    || arg.Value.StartsWith($"{argumentKey}<")
                                )
                            )
                )
                .ToImmutableList()
        };
    }

    /// <inheritdoc />
    public override string ToString()
    {
        // Prepends "pip install" to the arguments for clarity if used directly as a command string.
        // However, UvManager will call "uv" with "pip install" and then these arguments.
        // So, the base.ToString() which just joins arguments is usually what's needed by UvManager.
        return base.ToString();
    }
}
