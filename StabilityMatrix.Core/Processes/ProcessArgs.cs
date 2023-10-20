using System.Collections;
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

    /// <summary>
    /// Whether the argument string contains the given substring,
    /// or any of the given arguments if the input is an array.
    /// </summary>
    public bool Contains(string arg) => Match(str => str.Contains(arg), arr => arr.Any(Contains));

    /// <inheritdoc />
    public override string ToString()
    {
        return Match(str => str, arr => string.Join(' ', arr.Select(ProcessRunner.Quote)));
    }

    public string[] ToArray() =>
        Match(
            str => ArgumentsRegex().Matches(str).Select(x => x.Value.Trim('"')).ToArray(),
            arr => arr
        );

    // Implicit conversions

    public static implicit operator ProcessArgs(string input) => new(input);

    public static implicit operator ProcessArgs(string[] input) => new(input);

    public static implicit operator string(ProcessArgs input) => input.ToString();

    public static implicit operator string[](ProcessArgs input) => input.ToArray();

    [GeneratedRegex("""[\"].+?[\"]|[^ ]+""", RegexOptions.IgnoreCase)]
    private static partial Regex ArgumentsRegex();
}
