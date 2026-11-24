using Microsoft.Extensions.DependencyInjection;
using Refit;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Tests.Fakes;

namespace StabilityMatrix.Tests.Core;

/// <summary>
/// <see cref="ComfyClient"/> against <see cref="FakeComfyServer"/> replaying traffic recorded from a
/// real ComfyUI, over a real HTTP connection and websocket.
/// </summary>
[TestClass]
public class ComfyClientFakeServerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private FakeComfyServer server = null!;
    private ComfyClient client = null!;

    [TestInitialize]
    public async Task TestInitialize()
    {
        server = await FakeComfyServer.StartAsync();
        client = await ConnectAsync(server);
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        await client.CloseAsync();
        client.Dispose();
        await server.DisposeAsync();
    }

    internal static async Task<ComfyClient> ConnectAsync(FakeComfyServer server)
    {
        var httpClientFactory = new ServiceCollection()
            .AddHttpClient()
            .BuildServiceProvider()
            .GetRequiredService<IHttpClientFactory>();

        var comfyClient = new ComfyClient(new ApiFactory(httpClientFactory), server.BaseAddress);
        await comfyClient.ConnectAsync();
        return comfyClient;
    }

    private static Dictionary<string, ComfyNode> Graph() =>
        new()
        {
            ["1"] = new ComfyNode
            {
                ClassType = "EmptyImage",
                Inputs = new() { ["width"] = 64, ["height"] = 64 },
            },
        };

    [TestMethod]
    public async Task Prompt_CompletesWithProgressAndImages()
    {
        var task = await client.QueuePromptAsync(Graph());
        await task.Task.WaitAsync(Timeout);

        Assert.AreEqual(4, task.LastProgressUpdate?.Value);
        Assert.AreEqual(4, task.LastProgressUpdate?.Maximum);

        var posted = server.ReceivedPrompts.Single();
        Assert.AreEqual(client.ClientId, posted["client_id"]!.GetValue<string>());
        Assert.AreEqual(task.Id, posted["prompt_id"]!.GetValue<string>());

        var images = await client.GetImagesForExecutedPromptAsync(task.Id);
        Assert.AreEqual(4, images["4"]!.Count);

        await using var stream = await client.GetImageStreamAsync(images["4"]![0]);
        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes);
        CollectionAssert.AreEqual(server.Recording.ViewImage, bytes.ToArray());
    }

    /// <summary>
    /// ComfyUI can send a prompt's messages before its /prompt response, as when every node
    /// is cached. A client that only starts tracking the prompt once the response arrives
    /// never sees it finish.
    /// </summary>
    [TestMethod]
    public async Task Prompt_CompletesWhenMessagesArriveBeforeResponse()
    {
        server.ResponseDelay = TimeSpan.FromMilliseconds(300);

        var task = await client.QueuePromptAsync(Graph());

        await task.Task.WaitAsync(Timeout);
        Assert.IsTrue(client.PromptTasks.IsEmpty);
    }

    [TestMethod]
    public async Task Prompt_CompletesWhenServerAssignsItsOwnId()
    {
        var gate = new TaskCompletionSource();
        server.IgnoresClientPromptId = true;
        server.ReplayGate = gate.Task;

        var task = await client.QueuePromptAsync(Graph());
        gate.SetResult();

        await task.Task.WaitAsync(Timeout);
        Assert.AreNotEqual(server.ReceivedPrompts.Single()["prompt_id"]?.GetValue<string>(), task.Id);
        Assert.IsTrue(client.PromptTasks.IsEmpty);
    }

    [TestMethod]
    public async Task RuntimeError_FailsTaskWithNodeException()
    {
        server.PromptScript = "runtime_error";

        var task = await client.QueuePromptAsync(Graph());

        var exception = await Assert.ThrowsExceptionAsync<ComfyNodeException>(() =>
            task.Task.WaitAsync(Timeout)
        );
        Assert.AreEqual("CheckpointLoaderSimple", exception.ErrorData.NodeType);
        StringAssert.Contains(exception.ErrorData.ExceptionMessage, "header too small");
    }

    [TestMethod]
    public async Task ValidationError_ThrowsAndLeavesNoTask()
    {
        server.PromptScript = "validation_error";

        var exception = await Assert.ThrowsExceptionAsync<ApiException>(() =>
            client.QueuePromptAsync(Graph())
        );

        Assert.AreEqual(400, (int)exception.StatusCode);
        StringAssert.Contains(exception.Content, "prompt_outputs_failed_validation");
        Assert.IsTrue(client.PromptTasks.IsEmpty);
    }

    /// <summary>
    /// A handler exception used to end the websocket subscription for good: the client stayed
    /// connected but no later message reached it.
    /// </summary>
    [TestMethod]
    public async Task MessageThatFailsToParse_DoesNotStopLaterMessages()
    {
        server.EditReplay = (promptId, messages) =>
        {
            var fractional = $$$"""
                {"type":"progress","data":{"value":12.5,"max":20,"prompt_id":"{{{promptId}}}","node":"3"}}
                """;
            var firstProgress = messages.ToList().FindIndex(m => ComfyRecordedPrompt.TypeOf(m) == "progress");
            return messages.Take(firstProgress).Append(fractional).Concat(messages.Skip(firstProgress));
        };

        var task = await client.QueuePromptAsync(Graph());

        await task.Task.WaitAsync(Timeout);
        Assert.AreEqual(4, task.LastProgressUpdate?.Value);
    }

    [TestMethod]
    public async Task Prompt_CompletesAfterServerDropsWebsocket()
    {
        await server.DisconnectAsync(client.ClientId);

        // The replay waits for the client's websocket to reconnect
        var task = await client.QueuePromptAsync(Graph());
        await task.Task.WaitAsync(TimeSpan.FromSeconds(40));
    }

    [TestMethod]
    public async Task Interrupt_CancelsRunningPrompt()
    {
        var gate = new TaskCompletionSource();
        server.ReplayGate = gate.Task;
        var task = await client.QueuePromptAsync(Graph());

        await client.InterruptPromptAsync();
        gate.SetResult();

        Assert.AreEqual(1, server.InterruptCount);
        Assert.IsTrue(task.Task.IsCanceled);
        Assert.IsTrue(client.PromptTasks.IsEmpty);
    }
}
