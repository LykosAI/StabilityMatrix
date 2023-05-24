using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace StabilityMatrix;

/// <summary>
/// Implement the interface of the sys.stdout redirection
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal class PyIOStream
{
    public event EventHandler<string> OnWriteUpdate;
    public TextWriter TextWriter { get; set; }

    public PyIOStream(TextWriter writer = null)
    {
        TextWriter = writer ?? new StringWriter();
    }

    public void write(string str)
    {
        TextWriter.Write(str);
        OnWriteUpdate?.Invoke(this, str);
    }

    public void writelines(string[] str)
    {
        foreach (var line in str)
        {
            write(line);
        }
    }

    public void flush()
    {
        TextWriter.Flush();
    }

    public void close()
    {
        TextWriter?.Close();
    }
}