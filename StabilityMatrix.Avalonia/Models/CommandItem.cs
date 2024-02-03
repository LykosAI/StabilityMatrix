using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.Models;

public partial record CommandItem
{
    public ICommand Command { get; init; }

    public string DisplayName { get; init; }

    public CommandItem(ICommand command, [CallerArgumentExpression("command")] string? commandName = null)
    {
        Command = command;
        DisplayName = commandName == null ? "" : ProcessName(commandName);
    }

    [Pure]
    private static string ProcessName(string name)
    {
        name = name.StripEnd("Command");

        name = SpaceTitleCaseRegex().Replace(name, "$1 $2");

        return name;
    }

    [GeneratedRegex("([a-z])_?([A-Z])")]
    private static partial Regex SpaceTitleCaseRegex();
}
