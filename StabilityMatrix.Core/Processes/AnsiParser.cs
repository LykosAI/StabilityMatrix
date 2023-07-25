using System.Text.RegularExpressions;

namespace StabilityMatrix.Core.Processes;

public static partial class AnsiParser
{
    /// <summary>
    /// From https://github.com/chalk/ansi-regex
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"[\u001B\u009B][[\]()#;?]*(?:(?:(?:(?:;[-a-zA-Z\d\/#&.:=?%@~_]+)*|[a-zA-Z\d]+(?:;[-a-zA-Z\d\/#&.:=?%@~_]*)*)?\u0007)|(?:(?:\d{1,4}(?:;\d{0,4})*)?[\dA-PR-TZcf-nq-uy=><~]))")]
    public static partial Regex AnsiEscapeSequenceRegex();
}
