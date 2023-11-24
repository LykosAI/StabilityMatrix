using System.Diagnostics;
using System.Diagnostics.Contracts;
using OneOf;

namespace StabilityMatrix.Core.Processes;

/// <summary>
/// Builder for <see cref="ProcessArgs"/>.
/// </summary>
public record ProcessArgsBuilder
{
    protected ProcessArgsBuilder() { }

    public ProcessArgsBuilder(params Argument[] arguments)
    {
        Arguments = arguments.ToList();
    }

    public List<Argument> Arguments { get; init; } = new();

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

    /// <inheritdoc />
    public override string ToString()
    {
        return ToProcessArgs().ToString();
    }

    public ProcessArgs ToProcessArgs()
    {
        return ToStringArgs().ToArray();
    }

    public static implicit operator ProcessArgs(ProcessArgsBuilder builder) =>
        builder.ToProcessArgs();
}

public static class ProcessArgBuilderExtensions
{
    [Pure]
    public static T AddArg<T>(this T builder, Argument argument)
        where T : ProcessArgsBuilder
    {
        return builder with { Arguments = builder.Arguments.Append(argument).ToList() };
    }

    [Pure]
    public static T RemoveArgKey<T>(this T builder, string argumentKey)
        where T : ProcessArgsBuilder
    {
        return builder with
        {
            Arguments = builder.Arguments
                .Where(
                    x =>
                        x.Match(
                            stringArg => stringArg != argumentKey,
                            tupleArg => tupleArg.Item1 != argumentKey
                        )
                )
                .ToList()
        };
    }
}
