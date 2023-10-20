using System.Text.RegularExpressions;
using OneOf;

namespace StabilityMatrix.Core.Processes;

/// <summary>
/// Parameter type for command line arguments
/// Implicitly converts between string and string[],
/// with no parsing if the input and output types are the same.
/// </summary>
public partial class ProcessArgs : OneOfBase<string, string[]>
{
    /// <inheritdoc />
    private ProcessArgs(OneOf<string, string[]> input)
        : base(input) { }

    // Implicit conversions

    public static implicit operator ProcessArgs(string input) => new(input);

    public static implicit operator ProcessArgs(string[] input) => new(input);

    public static implicit operator string(ProcessArgs input) =>
        input.Match(str => str, arr => string.Join(' ', arr.Select(ProcessRunner.Quote)));

    public static implicit operator string[](ProcessArgs input) =>
        input.Match(
            str => ArgumentsRegex().Matches(str).Select(x => x.Value.Trim('"')).ToArray(),
            arr => arr
        );

    [GeneratedRegex("""[\"].+?[\"]|[^ ]+""", RegexOptions.IgnoreCase)]
    private static partial Regex ArgumentsRegex();
}
