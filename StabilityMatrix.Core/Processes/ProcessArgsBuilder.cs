using System.Collections.Immutable;
using System.Diagnostics.Contracts;

namespace StabilityMatrix.Core.Processes;

/// <summary>
/// Builder for <see cref="ProcessArgs"/>.
/// </summary>
public record ProcessArgsBuilder
{
    public ImmutableList<Argument> Arguments { get; init; } = ImmutableList<Argument>.Empty;

    public ProcessArgsBuilder(params Argument[] arguments)
    {
        Arguments = arguments.ToImmutableList();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return ToProcessArgs().ToString();
    }

    public ProcessArgs ToProcessArgs()
    {
        return new ProcessArgs(Arguments.ToImmutableArray());
    }

    public static implicit operator ProcessArgs(ProcessArgsBuilder builder) => builder.ToProcessArgs();
}

public static class ProcessArgBuilderExtensions
{
    [Pure]
    public static T AddArg<T>(this T builder, Argument argument)
        where T : ProcessArgsBuilder
    {
        return builder with { Arguments = builder.Arguments.Add(argument) };
    }

    [Pure]
    public static T AddArgs<T>(this T builder, params Argument[] argument)
        where T : ProcessArgsBuilder
    {
        return builder with { Arguments = builder.Arguments.AddRange(argument) };
    }

    /// <summary>
    /// Add arguments from strings using the given key.
    /// </summary>
    [Pure]
    public static T AddKeyedArgs<T>(this T builder, string key, IEnumerable<string> arguments)
        where T : ProcessArgsBuilder
    {
        return builder with
        {
            Arguments = builder.Arguments.AddRange(arguments.Select(arg => new Argument(key, arg)))
        };
    }

    [Pure]
    public static T UpdateArg<T>(this T builder, string key, Argument argument)
        where T : ProcessArgsBuilder
    {
        foreach (var arg in builder.Arguments)
        {
            if ((arg.Key ?? arg.Value) == key)
            {
                return builder with { Arguments = builder.Arguments.Replace(arg, argument) };
            }
        }

        // No match, add the new argument
        return builder.AddArg(argument);
    }

    [Pure]
    public static T RemoveArgKey<T>(this T builder, string argumentKey)
        where T : ProcessArgsBuilder
    {
        return builder with
        {
            Arguments = builder
                .Arguments.Where(arg => (arg.Key ?? arg.Value) != argumentKey)
                .ToImmutableList()
        };
    }
}
