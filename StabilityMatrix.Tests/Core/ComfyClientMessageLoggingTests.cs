using System.Runtime.CompilerServices;
using System.Text.Json;
using NLog;
using NLog.Config;
using NLog.Targets;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Tests.Core;

[TestClass]
[DoNotParallelize]
public class ComfyClientMessageLoggingTests
{
    private LoggingConfiguration? previousConfiguration;
    private MemoryTarget memory = null!;

    [TestInitialize]
    public void Setup()
    {
        previousConfiguration = LogManager.Configuration;

        memory = new MemoryTarget("memory") { Layout = "${level}|${message}" };
        var config = new LoggingConfiguration();
        config.AddRule(LogLevel.Trace, LogLevel.Fatal, memory, typeof(ComfyClient).FullName!);
        LogManager.Configuration = config;
    }

    [TestCleanup]
    public void Cleanup()
    {
        LogManager.Configuration = previousConfiguration;
    }

    private static ComfyClient CreateClient() =>
        (ComfyClient)RuntimeHelpers.GetUninitializedObject(typeof(ComfyClient));

    private string[] LogsAtOrAbove(LogLevel level) =>
        memory.Logs.Where(l => LogLevel.FromString(l.Split('|')[0]) >= level).ToArray();

    [DataTestMethod]
    [DataRow("""{"type":"execution_start","data":{"prompt_id":"p1","timestamp":1}}""")]
    [DataRow("""{"type":"execution_cached","data":{"nodes":["1","2"],"prompt_id":"p1"}}""")]
    [DataRow("""{"type":"executed","data":{"node":"9","output":{"images":[]},"prompt_id":"p1"}}""")]
    [DataRow("""{"type":"execution_success","data":{"prompt_id":"p1","timestamp":2}}""")]
    [DataRow("""{"type":"execution_interrupted","data":{"prompt_id":"p1","node_id":"3"}}""")]
    [DataRow(
        """{"type":"progress_state","data":{"prompt_id":"p1","nodes":{"3":{"value":1,"max":20,"state":"running"}}}}"""
    )]
    [DataRow("""{"type":"crystools.monitor","data":{"cpu_utilization":12.5}}""")]
    public void HandleTextMessage_UnhandledTypes_DoNotLogAboveTrace(string message)
    {
        CreateClient().HandleTextMessage(message);
        LogManager.Flush();

        CollectionAssert.AreEqual(Array.Empty<string>(), LogsAtOrAbove(LogLevel.Debug));
    }

    [TestMethod]
    public void HandleTextMessage_CustomNodeType_LogsTypeWithoutPayloadAtTrace()
    {
        CreateClient().HandleTextMessage("""{"type":"crystools.monitor","data":{"cpu_utilization":12.5}}""");
        LogManager.Flush();

        var skipped = memory.Logs.Single(l => l.Contains("Skipping unhandled message type"));
        StringAssert.StartsWith(skipped, "Trace|");
        Assert.IsFalse(skipped.Contains("cpu_utilization"));
    }

    [TestMethod]
    public void HandleTextMessage_InvalidJson_StillWarns()
    {
        CreateClient().HandleTextMessage("not json");
        LogManager.Flush();

        Assert.AreEqual(1, LogsAtOrAbove(LogLevel.Warn).Length);
    }

    [DataTestMethod]
    [DataRow("execution_success", ComfyWebSocketResponseType.ExecutionSuccess)]
    [DataRow("execution_interrupted", ComfyWebSocketResponseType.ExecutionInterrupted)]
    [DataRow("progress_state", ComfyWebSocketResponseType.ProgressState)]
    [DataRow("crystools.monitor", ComfyWebSocketResponseType.Unknown)]
    public void ResponseType_DeserializesNewTypes(string type, ComfyWebSocketResponseType expected)
    {
        var response = JsonSerializer.Deserialize<ComfyWebSocketResponse>(
            $$$"""{"type":"{{{type}}}","data":{}}"""
        );

        Assert.AreEqual(expected, response!.Type);
    }
}
