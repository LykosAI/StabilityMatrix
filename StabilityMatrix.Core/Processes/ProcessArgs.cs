using System.Collections;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using OneOf;

namespace StabilityMatrix.Core.Processes;

/// <summary>
/// Parameter type for command line arguments
/// Implicitly converts between string and string[],
/// with no parsing if the input and output types are the same.
/// </summary>
[Localizable(false)]
[CollectionBuilder(typeof(ProcessArgsCollectionBuilder), "Create")]
public partial class ProcessArgs : OneOfBase<string, ImmutableArray<Argument>>, IEnumerable<Argument>
{
    public static ProcessArgs Empty { get; } = new(ImmutableArray<Argument>.Empty);

    /// <summary>
    /// Create a new <see cref="ProcessArgs"/> from pre-quoted argument parts,
    /// which may contain spaces or multiple arguments.
    /// </summary>
    /// <param name="inputs">Quoted string arguments</param>
    /// <returns>A new <see cref="ProcessArgs"/> instance</returns>
    public static ProcessArgs FromQuoted(IEnumerable<string> inputs)
    {
        var args = inputs.Select(Argument.Quoted).ToImmutableArray();
        return new ProcessArgs(args);
    }

    /*public ProcessArgs(string arguments)
        : base(arguments) { }
    
    public ProcessArgs(IEnumerable<Argument> arguments)
        : base(arguments.ToImmutableArray()) { }*/

    public ProcessArgs(OneOf<string, ImmutableArray<Argument>> input)
        : base(input) { }

    /// <summary>
    /// Whether the argument string contains the given substring,
    /// or any of the given arguments if the input is an array.
    /// </summary>
    public bool Contains(string argument) =>
        Match(
            str => str.Contains(argument),
            arr => arr.Any(arg => arg.Value == argument || arg.Key == argument)
        );

    [Pure]
    public ProcessArgs Concat(ProcessArgs other) =>
        Match(
            str => new ProcessArgs(string.Join(' ', str, other.ToString())),
            argsArray => new ProcessArgs(argsArray.AddRange(other.ToArgumentArray()))
        );

    [Pure]
    public ProcessArgs Prepend(ProcessArgs other) =>
        Match(
            str => new ProcessArgs(string.Join(' ', other.ToString(), str)),
            argsArray => new ProcessArgs(other.ToArgumentArray().AddRange(argsArray))
        );

    /// <summary>
    /// Gets a process string representation for command line execution.
    /// </summary>
    [Pure]
    public override string ToString()
    {
        return Match(
            str => str,
            argsArray => string.Join(' ', argsArray.Select(arg => arg.GetQuotedValue()))
        );
    }

    /// <summary>
    /// Gets an immutable array of <see cref="Argument"/> instances.
    /// </summary>
    [Pure]
    public ImmutableArray<Argument> ToArgumentArray() =>
        Match(str => [..ParseArguments(str)], argsArray => argsArray);

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IEnumerator<Argument> GetEnumerator()
    {
        return ToArgumentArray().AsEnumerable().GetEnumerator();
    }

    /// <summary>
    /// Parses the input string into <see cref="Argument"/> instances.
    /// </summary>
    private static IEnumerable<Argument> ParseArguments(string input) =>
        ArgumentsRegex().Matches(input).Select(match => new Argument(match.Value.Trim('"')));

    [GeneratedRegex("""[\"].+?[\"]|[^ ]+""", RegexOptions.IgnoreCase)]
    private static partial Regex ArgumentsRegex();

    // Implicit (string -> ProcessArgs)
    public static implicit operator ProcessArgs(string input) => new(input);

    // Implicit (string[] -> Argument[] -> ProcessArgs)
    public static implicit operator ProcessArgs(string[] input) =>
        new(input.Select(x => new Argument(x)).ToImmutableArray());

    // Implicit (Argument[] -> ProcessArgs)
    public static implicit operator ProcessArgs(Argument[] input) => new(input.ToImmutableArray());

    // Implicit (ProcessArgs -> string)
    public static implicit operator string(ProcessArgs input) => input.ToString();
}

[Localizable(false)]
public static class ProcessArgsCollectionBuilder
{
    public static ProcessArgs Create(ReadOnlySpan<Argument> values) => new(values.ToImmutableArray());
}
