using System.Collections.Immutable;
using System.Diagnostics.Contracts;

namespace StabilityMatrix.Core.Processes;

/// <summary>
/// Builder for <see cref="ProcessArgs"/>.
/// </summary>
public record ProcessArgsBuilder
{
    public IImmutableList<Argument> Arguments { get; init; } = ImmutableArray<Argument>.Empty;

    private IEnumerable<string> ToStringArgs()
    {
        foreach (var argument in Arguments)
        {
            if (argument.IsT0)
            {
                yield return argument.AsT0;
            }
            else
            {
                yield return argument.AsT1.Item1;
                yield return argument.AsT1.Item2;
            }
        }
    }

    public ProcessArgsBuilder(params Argument[] arguments)
    {
        Arguments = arguments.ToImmutableArray();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return ToProcessArgs().ToString();
    }

    public ProcessArgs ToProcessArgs()
    {
        return ToStringArgs().ToArray();
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

    [Pure]
    public static T UpdateArg<T>(this T builder, string key, Argument argument)
        where T : ProcessArgsBuilder
    {
        var oldArg = builder.Arguments.FirstOrDefault(
            x => x.Match(stringArg => stringArg == key, tupleArg => tupleArg.Item1 == key)
        );

        if (oldArg is null)
        {
            return builder.AddArg(argument);
        }

        return builder with
        {
            Arguments = builder.Arguments.Replace(oldArg, argument)
        };
    }

    [Pure]
    public static T RemoveArgKey<T>(this T builder, string argumentKey)
        where T : ProcessArgsBuilder
    {
        return builder with
        {
            Arguments = builder
                .Arguments.Where(
                    x =>
                        x.Match(
                            stringArg => stringArg != argumentKey,
                            tupleArg => tupleArg.Item1 != argumentKey
                        )
                )
                .ToImmutableArray()
        };
    }

    [Pure]
    public static T RemovePipArgKey<T>(this T builder, string argumentKey)
        where T : ProcessArgsBuilder
    {
        return builder with
        {
            Arguments = builder
                .Arguments.Where(
                    x =>
                        x.Match(
                            stringArg =>
                                !stringArg.Contains($"{argumentKey}==") && !stringArg.Equals(argumentKey),
                            tupleArg =>
                                !tupleArg.Item1.Contains($"{argumentKey}==") && tupleArg.Item1 != argumentKey
                        )
                )
                .ToImmutableArray()
        };
    }
}
