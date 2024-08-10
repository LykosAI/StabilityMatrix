using System.Collections;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using OneOf;

namespace StabilityMatrix.Core.Processes;

/// <summary>
/// Parameter type for command line arguments
/// Implicitly converts between string and string[],
/// with no parsing if the input and output types are the same.
/// </summary>
[CollectionBuilder(typeof(ProcessArgsCollectionBuilder), "Create")]
public partial class ProcessArgs : OneOfBase<string, string[]>, IEnumerable<string>
{
    /// <summary>
    /// Create a new <see cref="ProcessArgs"/> from pre-quoted argument parts,
    /// which may contain spaces or multiple arguments.
    /// </summary>
    /// <param name="inputs">Quoted string arguments</param>
    /// <returns>A new <see cref="ProcessArgs"/> instance</returns>
    public static ProcessArgs FromQuoted(IEnumerable<string> inputs)
    {
        ProcessArgs? result = null;

        foreach (var input in inputs)
        {
            result =
                result?.Concat(new ProcessArgs(OneOf<string, string[]>.FromT0(input)))
                ?? new ProcessArgs(OneOf<string, string[]>.FromT0(input));
        }

        return result ?? new ProcessArgs(OneOf<string, string[]>.FromT1([]));
    }

    /// <inheritdoc />
    public ProcessArgs(OneOf<string, string[]> input)
        : base(input) { }

    /// <summary>
    /// Whether the argument string contains the given substring,
    /// or any of the given arguments if the input is an array.
    /// </summary>
    public bool Contains(string arg) => Match(str => str.Contains(arg), arr => arr.Any(x => x.Contains(arg)));

    public ProcessArgs Concat(ProcessArgs other) =>
        Match(
            str => new ProcessArgs(string.Join(' ', str, other.ToString())),
            arr => new ProcessArgs(arr.Concat(other.ToArray()).ToArray())
        );

    public ProcessArgs Prepend(ProcessArgs other) =>
        Match(
            str => new ProcessArgs(string.Join(' ', other.ToString(), str)),
            arr => new ProcessArgs(other.ToArray().Concat(arr).ToArray())
        );

    /// <inheritdoc />
    public IEnumerator<string> GetEnumerator()
    {
        return ToArray().AsEnumerable().GetEnumerator();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Match(str => str, arr => string.Join(' ', arr.Select(ProcessRunner.Quote)));
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public string[] ToArray() =>
        Match(str => ArgumentsRegex().Matches(str).Select(x => x.Value.Trim('"')).ToArray(), arr => arr);

    // Implicit conversions

    public static implicit operator ProcessArgs(string input) => new(input);

    public static implicit operator ProcessArgs(string[] input) => new(input);

    public static implicit operator string(ProcessArgs input) => input.ToString();

    public static implicit operator string[](ProcessArgs input) => input.ToArray();

    [GeneratedRegex("""[\"].+?[\"]|[^ ]+""", RegexOptions.IgnoreCase)]
    private static partial Regex ArgumentsRegex();
}

public static class ProcessArgsCollectionBuilder
{
    public static ProcessArgs Create(ReadOnlySpan<string> values) => new(values.ToArray());
}
