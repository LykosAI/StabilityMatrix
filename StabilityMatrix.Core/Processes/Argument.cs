using System.Diagnostics.CodeAnalysis;

namespace StabilityMatrix.Core.Processes;

/// <summary>
/// Represents a command line argument.
/// </summary>
public readonly record struct Argument
{
    /// <summary>
    /// The value of the argument.
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Optional key for the argument.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// Whether the <see cref="Key"/> property is set and not empty.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Key))]
    public bool HasKey => !string.IsNullOrEmpty(Key);

    /// <summary>
    /// Whether the argument value is already quoted for command line usage.
    /// </summary>
    public bool IsQuoted { get; init; }

    /// <summary>
    /// Gets the value with quoting if necessary.
    /// Is equal to <see cref="Value"/> if <see cref="IsQuoted"/> is <see langword="true"/>.
    /// </summary>
    /// <returns></returns>
    public string GetQuotedValue() => IsQuoted ? Value : ProcessRunner.Quote(Value);

    /// <summary>
    /// Create a new argument with the given pre-quoted value.
    /// </summary>
    public static Argument Quoted(string value) => new(value) { IsQuoted = true };

    /// <summary>
    /// Create a new keyed argument with the given pre-quoted value.
    /// </summary>
    public static Argument Quoted(string key, string value) => new(key, value) { IsQuoted = true };

    public Argument() { }

    public Argument(string value)
    {
        Value = value;
    }

    public Argument(string key, string value)
    {
        Value = value;
        Key = key;
    }

    // Implicit (string -> Argument)
    public static implicit operator Argument(string _) => new(_);

    // Explicit (Argument -> string)
    public static explicit operator string(Argument _) => _.Value;

    // Implicit ((string, string) -> Argument)
    public static implicit operator Argument((string Key, string Value) _) => new(_.Key, _.Value);
}
