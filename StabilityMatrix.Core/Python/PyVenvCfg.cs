using System.Text;

namespace StabilityMatrix.Core.Python;

/// <summary>
/// Ordered, sectionless <c>key = value</c> configuration, as used by pyvenv.cfg.
/// Keys are case-insensitive. Duplicate keys are preserved in order; setting a
/// key rewrites every occurrence (fixing stale duplicates) rather than only the
/// first, which matches how CPython's site.py actually reads the file.
/// </summary>
public sealed class PyVenvCfg
{
    private readonly List<Entry> _entries;

    private PyVenvCfg(List<Entry> entries) => _entries = entries;

    /// <summary>Parses pyvenv.cfg text without touching the disk.</summary>
    public static PyVenvCfg Parse(string content)
    {
        var entries = new List<Entry>();

        var segments = content.Split('\n');
        // A trailing empty segment is the artifact of a final newline, not a real line.
        var lineCount =
            segments.Length > 0 && segments[^1].Length == 0 ? segments.Length - 1 : segments.Length;

        for (var i = 0; i < lineCount; i++)
        {
            var text = segments[i].TrimEnd('\r');
            var trimmed = text.Trim();
            var eqIdx = trimmed.IndexOf('=');

            // Lines without '=' are comments/blank lines and are preserved as-is.
            if (eqIdx < 0)
            {
                entries.Add(new Entry(text, null, null));
                continue;
            }

            var key = trimmed[..eqIdx].Trim();
            var value = trimmed[(eqIdx + 1)..].Trim();
            entries.Add(new Entry(text, key, value));
        }

        return new PyVenvCfg(entries);
    }

    /// <summary>
    /// Loads a pyvenv.cfg file. Fails loudly on non-UTF-8 encodings instead of
    /// silently mangling the file.
    /// </summary>
    public static PyVenvCfg Load(string path)
    {
        var bytes = File.ReadAllBytes(path);

        // pyvenv.cfg is UTF-8/ASCII; reject UTF-16 BOMs and NUL bytes, which
        // indicate the file was read with the wrong encoding.
        if (
            bytes.Length >= 2
            && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF))
        )
        {
            throw new InvalidDataException($"pyvenv.cfg is UTF-16 encoded; expected UTF-8/ASCII: {path}");
        }

        var content = new UTF8Encoding(false).GetString(bytes);
        if (content.Contains('\0'))
        {
            throw new InvalidDataException($"pyvenv.cfg contains NUL bytes; expected UTF-8/ASCII: {path}");
        }

        return Parse(content);
    }

    /// <summary>
    /// Gets the value of the last matching key (CPython is last-wins), or null.
    /// Setting rewrites every matching key, appending a new key when absent.
    /// </summary>
    public string? this[string key]
    {
        get
        {
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].Key is { } k && k.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return _entries[i].Value;
                }
            }

            return null;
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            var updated = false;
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Key is { } k && k.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    _entries[i].Text = $"{key} = {value}";
                    _entries[i].Value = value;
                    updated = true;
                }
            }

            if (!updated)
            {
                _entries.Add(new Entry($"{key} = {value}", key, value));
            }
        }
    }

    /// <summary>Serializes back to pyvenv.cfg text, preserving order and untouched lines.</summary>
    public override string ToString() => string.Join(Environment.NewLine, _entries.Select(e => e.Text));

    /// <summary>Writes the config back to disk.</summary>
    public void Save(string path) => File.WriteAllText(path, ToString());

    private sealed class Entry
    {
        public Entry(string text, string? key, string? value)
        {
            Text = text;
            Key = key;
            Value = value;
        }

        public string Text { get; set; }

        public string? Key { get; }

        public string? Value { get; set; }
    }
}
