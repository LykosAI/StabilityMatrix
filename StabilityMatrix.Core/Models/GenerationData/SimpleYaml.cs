using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace StabilityMatrix.Core.Models.GenerationData;

/// <summary>
/// Parses the small subset of YAML that tools use to write flat or nested "key: value" records.
/// </summary>
internal static partial class SimpleYaml
{
    public static JsonObject? Parse(string text)
    {
        var root = new JsonObject();
        var stack = new Stack<(int Indent, JsonObject Object)>();
        stack.Push((-1, root));
        var lines = text.Replace("\r\n", "\n").Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                continue;
            var indent = IndentOf(line);
            var match = KeyValueRegex().Match(line.Trim());
            if (!match.Success)
                return null;

            while (stack.Count > 1 && indent <= stack.Peek().Indent)
                stack.Pop();
            var parent = stack.Peek().Object;
            var key = match.Groups[1].Value;
            var rawValue = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;

            if (rawValue.Length == 0)
            {
                var child = new JsonObject();
                parent[key] = child;
                stack.Push((indent, child));
                continue;
            }

            var block = BlockScalarRegex().Match(rawValue);
            if (!block.Success)
            {
                parent[key] = ParseScalar(rawValue);
                continue;
            }

            // block scalar: the following lines that are indented deeper, or empty
            var body = new List<string>();
            while (
                i + 1 < lines.Length
                && (string.IsNullOrWhiteSpace(lines[i + 1]) || IndentOf(lines[i + 1]) > indent)
            )
                body.Add(lines[++i]);
            while (body.Count > 0 && string.IsNullOrWhiteSpace(body[^1]))
                body.RemoveAt(body.Count - 1);

            var nonEmpty = body.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            var bodyIndent = nonEmpty.Count == 0 ? 0 : nonEmpty.Min(IndentOf);
            var content = body.Select(l => l.Length >= bodyIndent ? l[bodyIndent..] : string.Empty).ToList();

            string value;
            if (block.Groups[1].Value == "|")
            {
                value = string.Join('\n', content);
            }
            else
            {
                var folded = new StringBuilder();
                for (var j = 0; j < content.Count; j++)
                {
                    if (content[j].Length == 0)
                        folded.Append('\n');
                    else
                        folded.Append(j > 0 && content[j - 1].Length > 0 ? " " : "").Append(content[j]);
                }
                value = folded.ToString();
            }
            parent[key] = block.Groups[2].Value == "-" ? value : value + "\n";
        }

        return root;
    }

    private static int IndentOf(string line) => line.Length - line.TrimStart().Length;

    private static JsonNode? ParseScalar(string value)
    {
        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
            return value[1..^1].Replace("''", "'");
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            try
            {
                return JsonSerializer.Deserialize<string>(value);
            }
            catch (JsonException)
            {
                return value[1..^1];
            }
        }
        if (value is "true" or "false")
            return value == "true";
        if (value is "null" or "~")
            return null;
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            return integer;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return number;
        return value;
    }

    [GeneratedRegex(@"^([^:]+?):(?:\s+(.*))?$")]
    private static partial Regex KeyValueRegex();

    [GeneratedRegex(@"^([>|])([+-]?)$")]
    private static partial Regex BlockScalarRegex();
}
