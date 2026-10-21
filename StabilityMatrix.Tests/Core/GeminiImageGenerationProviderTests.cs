using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Gemini;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class GeminiImageGenerationProviderTests
{
    [TestMethod]
    public async Task GenerateAsync_CancelledByCaller_ThrowsInsteadOfReportingFailure()
    {
        using var cts = new CancellationTokenSource();
        var geminiApi = Substitute.For<IGeminiApi>();
        geminiApi
            .GenerateContentAsync(
                Arg.Any<string>(),
                Arg.Any<GeminiGenerateContentRequest>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ =>
            {
                cts.Cancel();
                return Task.FromException<GeminiGenerateContentResponse>(
                    new TaskCanceledException("A task was canceled.")
                );
            });
        var secretsManager = Substitute.For<ISecretsManager>();
        secretsManager.SafeLoadAsync().Returns(new Secrets { GeminiApiKey = "test-key" });
        var provider = new Gemini3ProImageGenerationProvider(
            Substitute.For<ILogger<Gemini3ProImageGenerationProvider>>(),
            geminiApi,
            secretsManager
        );

        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() =>
            provider.GenerateAsync(new ImageGenerationRequest { TextPrompt = "a wolf" }, cts.Token)
        );
    }
}
