using System.Text;
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
    [DataRow("a\r\nb\nc", "a\r\n", "b\n", "c")]
    // Carriage returns \r should be sent as is
    [DataRow("a\rb\rc", "a\rb\rc")]
    [DataRow("a1\ra2\nb1\rb2", "a1\ra2\n", "b1\rb2")]
    public async Task TestRead(string source, params string[] expected)
    {
        var queue = new Queue<string?>(expected);
        
        var callback = new Action<string?>(s =>
        {
            Assert.IsTrue(queue.Count > 0);
            Assert.AreEqual(queue.Dequeue(), s);
        });
        
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(source));
        
        // Make the reader
        using var reader = new AsyncStreamReader(stream, callback, Encoding.UTF8);
        
        // Begin read line and wait until finish
        reader.BeginReadLine();
        await reader.EOF;
        
        // Check if all expected strings were read
        Assert.AreEqual(0, queue.Count);
    }
}
