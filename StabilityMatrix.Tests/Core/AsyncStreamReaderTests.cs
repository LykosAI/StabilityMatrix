using System.Text;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Tests.Core;

/// <summary>
/// Tests AsyncStreamReader and ApcMessage parsing
/// </summary>
[TestClass]
public class AsyncStreamReaderTests
{
    
    [DataTestMethod]
    // Test newlines handling for \r\n, \n
    [DataRow("a\r\nb\nc", "a\r\n", "b\n", "c", null)]
    // Carriage returns \r should be sent as is
    [DataRow("a\rb\rc", "a", "\rb", "\rc", null)]
    [DataRow("a1\ra2\nb1\rb2", "a1", "\ra2\n", "b1", "\rb2", null)]
    // Ansi escapes should be seperated
    [DataRow("\x1b[A\x1b[A", "\x1b[A", "\x1b[A", null)]
    // Mixed Ansi and newlines
    [DataRow("a \x1b[A\r\n\r xyz", "a ", "\x1b[A", "\r\n", "\r xyz", null)]
    public async Task TestRead(string source, params string?[] expected)
    {
        var results = new List<string?>();
        
        var callback = new Action<string?>(s =>
        {
            results.Add(s);
        });
        
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(source));
        using (var reader = new AsyncStreamReader(stream, callback, Encoding.UTF8))
        {
            // Begin read line and wait until finish
            reader.BeginReadLine();
            // Wait for maximum 1 second
            await reader.EOF.WaitAsync(new CancellationTokenSource(1000).Token);
        }

        // Check expected output matches
        Assert.IsTrue(expected.SequenceEqual(results.ToArray()), 
            "Results [{0}] do not match expected [{1}]", 
            string.Join(", ", results.Select(s => s?.ToRepr() ?? "<null>")), 
            string.Join(", ", expected.Select(s => s?.ToRepr() ?? "<null>")));
    }
    
    [TestMethod]
    public async Task TestCarriageReturnHandling()
    {
        var expected = new[] {"dog\r\n", "cat", "\r123", "\r456", null};
        
        var results = new List<string?>();
        
        var callback = new Action<string?>(s =>
        {
            results.Add(s);
        });
        
        // The previous buffer should be sent when \r is encountered
        const string source = "dog\r\ncat\r123\r456";
        
        // Make the reader
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(source));
        using (var reader = new AsyncStreamReader(stream, callback, Encoding.UTF8))
        {
            // Begin read line and wait until finish
            reader.BeginReadLine();
            // Wait for maximum 1 second
            await reader.EOF.WaitAsync(new CancellationTokenSource(1000).Token);
        }
        
        // Check if all expected strings were read
        Assert.IsTrue(expected.SequenceEqual(results.ToArray()), 
            "Results [{0}] do not match expected [{1}]", 
            string.Join(", ", results.Select(s => s?.ToRepr() ?? "<null>")), 
            string.Join(", ", expected.Select(s => s?.ToRepr() ?? "<null>")));
    }
}
