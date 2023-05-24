using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace StabilityMatrix;

/// <summary>
/// Implement the interface of the sys.stdout redirection
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal class PyIOStream
{
    public event EventHandler<string> OnWriteUpdate;
    private readonly StringBuilder TextBuilder;
    private readonly StringWriter TextWriter;

    public PyIOStream(StringBuilder builder = null)
    {
        TextBuilder = builder ?? new StringBuilder();
        TextWriter = new StringWriter(TextBuilder);
    }

    public void ClearBuffer()
    {
        TextBuilder.Clear();
    }
    
    public string GetBuffer()
    {
        return TextBuilder.ToString();
    }

    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public void write(string str)
    {
        TextWriter.Write(str);
        OnWriteUpdate?.Invoke(this, str);
    }

    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public void writelines(IEnumerable<string> str)
    {
        foreach (var line in str)
        {
            write(line);
        }
    }

    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public void flush()
    {
        TextWriter.Flush();
    }

    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public void close()
    {
        TextWriter?.Close();
    }
}