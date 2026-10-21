using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Models.Configs;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class ImageGenerationChatServiceTests
{
    [TestMethod]
    public async Task RetryGenerationAsync_WithOldResponse_SendsOnlyMessagesBeforeThePrompt()
    {
        var settingsManager = Substitute.For<ISettingsManager>();
        settingsManager.LibraryDir.Returns(new DirectoryPath(Path.GetTempPath()));
        using var database = new BananaVisionDbContext(
            NullLogger<BananaVisionDbContext>.Instance,
            settingsManager,
            Options.Create(new DebugOptions { TempDatabase = true })
        );

        ImageGenerationRequest? sentRequest = null;
        var provider = Substitute.For<IImageGenerationProvider>();
        provider.ProviderId.Returns("test");
        provider
            .GenerateAsync(Arg.Do<ImageGenerationRequest>(r => sentRequest = r), Arg.Any<CancellationToken>())
            .Returns(new ImageGenerationResponse { IsSuccess = true, TextResponse = "new reply" });

        var service = new ImageGenerationChatService(
            NullLogger<ImageGenerationChatService>.Instance,
            database,
            settingsManager,
            [provider]
        );

        var conversation = await service.CreateConversationAsync("test");
        var t0 = DateTime.UtcNow.AddMinutes(-10);
        foreach (
            var (role, text, minutes) in new[]
            {
                (MessageRole.User, "first prompt", 0),
                (MessageRole.Assistant, "first reply", 1),
                (MessageRole.User, "second prompt", 2),
                (MessageRole.Assistant, "reply being regenerated", 3),
            }
        )
        {
            await database.Messages.InsertAsync(
                new ImageGenerationMessage
                {
                    ConversationId = conversation.Id,
                    Role = role,
                    TextContent = text,
                    Timestamp = t0.AddMinutes(minutes),
                }
            );
        }

        await service.RetryGenerationAsync(conversation.Id, "test");

        Assert.IsNotNull(sentRequest);
        Assert.AreEqual("second prompt", sentRequest.TextPrompt);
        CollectionAssert.AreEqual(
            new[] { "first prompt", "first reply" },
            sentRequest.ConversationHistory!.Select(m => m.TextContent).ToArray()
        );
    }
}
