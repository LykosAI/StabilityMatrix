using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.UITests;

/// <summary>
/// Image Lab page hosted headlessly over an in-memory chat service, so generation outcomes can be
/// timed against conversation switches.
/// </summary>
[Collection("TempDir")]
public class BananaVisionPageTests
{
    [AvaloniaFact]
    public async Task Generation_FinishingWhileAnotherConversationIsOpen_StaysOutOfIt()
    {
        foreach (var fails in new[] { false, true })
        {
            var lab = new ImageLab();
            var a = lab.AddConversation("A", minutesAgo: 1, "A prompt", "A reply");
            var b = lab.AddConversation("B", minutesAgo: 30, "B prompt", "B reply");
            var generation = lab.HoldSend(fails ? new ImageGenerationException("boom") : null);
            await lab.OpenAsync(a);

            lab.Vm.NewMessageText = "A second prompt";
            var send = lab.Vm.SendMessageCommand.ExecuteAsync(null);
            await Settle();
            await lab.SelectAsync(b);

            generation.SetResult();
            await send;
            await Settle();

            Assert.Equal("B", lab.Vm.CurrentConversation?.Title);
            Assert.Equal(new[] { "B prompt", "B reply" }, lab.Texts());
            Assert.False(lab.Vm.CanRetryLastMessage);

            await lab.SelectAsync(a);
            Assert.Equal(
                fails
                    ? new[] { "A prompt", "A reply", "A second prompt" }
                    : new[] { "A prompt", "A reply", "A second prompt", "sent reply" },
                lab.Texts()
            );
            Assert.Equal(fails, lab.Vm.CanRetryLastMessage);
            Assert.All(lab.Vm.Messages.OfType<TextMessage>(), m => Assert.True(m.CanDelete));
            lab.Close();
        }
    }

    [AvaloniaFact]
    public async Task Regenerate_KeepsTheOldResponseUntilTheNewOneIsSaved()
    {
        var lab = new ImageLab();
        var a = lab.AddConversation("A", minutesAgo: 1, "prompt", "old reply");
        await lab.OpenAsync(a);

        lab.FailRetry = true;
        await lab.Vm.RegenerateLastResponseCommand.ExecuteAsync(null);
        await Settle();
        Assert.Equal(new[] { "prompt", "old reply" }, lab.Texts());
        Assert.True(lab.Vm.CanRetryLastMessage);

        lab.FailRetry = false;
        await lab.Vm.RegenerateLastResponseCommand.ExecuteAsync(null);
        await Settle();
        Assert.Equal(new[] { "prompt", "regenerated reply" }, lab.Texts());
        lab.Close();
    }

    [AvaloniaFact]
    public async Task NewMessage_WhenAtTheBottom_ScrollsToIt()
    {
        var lab = new ImageLab();
        var a = lab.AddConversation("A", minutesAgo: 1);
        for (var i = 0; i < 12; i++)
        {
            lab.AddMessages(a, $"prompt {i} " + new string('x', 400), $"reply {i}");
        }
        lab.HoldSend(null);
        await lab.OpenAsync(a);

        var scroll = lab.Page.FindControl<ScrollViewer>("MessageScrollViewer")!;
        Assert.Equal(scroll.Extent.Height - scroll.Viewport.Height, scroll.Offset.Y, 1);

        lab.Vm.NewMessageText = "one more";
        _ = lab.Vm.SendMessageCommand.ExecuteAsync(null);
        await Settle();

        // The loading placeholder alone is taller than the near-bottom threshold
        Assert.Equal(scroll.Extent.Height - scroll.Viewport.Height, scroll.Offset.Y, 1);
        lab.Close();
    }

    private static async Task Settle()
    {
        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(30);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private sealed class ImageLab
    {
        private readonly List<ImageGenerationConversation> conversations = [];
        private readonly Dictionary<Guid, List<ImageGenerationMessage>> messages = [];
        private readonly IImageGenerationChatService chatService =
            Substitute.For<IImageGenerationChatService>();
        private Window? window;

        public ImageLab()
        {
            chatService.GetAvailableProviders().Returns([]);
            chatService
                .GetConversationsAsync()
                .Returns(_ =>
                    conversations.OrderByDescending(c => c.UpdatedAt).Select(c => c with { }).ToList()
                );
            chatService.GetMessagesAsync(Arg.Any<Guid>()).Returns(ci => messages[ci.Arg<Guid>()].ToList());
            chatService
                .DeleteMessageAsync(Arg.Any<Guid>(), Arg.Any<bool>())
                .Returns(ci =>
                {
                    foreach (var list in messages.Values)
                        list.RemoveAll(m => m.Id == ci.ArgAt<Guid>(0));
                    return Task.CompletedTask;
                });
            chatService
                .RetryGenerationAsync(
                    Arg.Any<Guid>(),
                    Arg.Any<string>(),
                    Arg.Any<Dictionary<string, object>?>(),
                    Arg.Any<IProgress<ImageGenerationProgress>?>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(ci =>
                {
                    if (FailRetry)
                        return Task.FromException<ImageGenerationMessage>(
                            new ImageGenerationException("failed")
                        );
                    Touch(ci.ArgAt<Guid>(0));
                    return Task.FromResult(
                        Save(ci.ArgAt<Guid>(0), MessageRole.Assistant, "regenerated reply")
                    );
                });

            Vm = new BananaVisionPageViewModel(
                NullLogger<BananaVisionPageViewModel>.Instance,
                chatService,
                Substitute.For<ISecretsManager>(),
                Substitute.For<INotificationService>(),
                Substitute.For<IInferenceClientManager>(),
                new RunningPackageService(
                    NullLogger<RunningPackageService>.Instance,
                    Substitute.For<IPackageFactory>(),
                    Substitute.For<INotificationService>(),
                    Substitute.For<ISettingsManager>(),
                    Substitute.For<IPyRunner>()
                ),
                Substitute.For<IServiceManager<ViewModelBase>>(),
                Substitute.For<IModelIndexService>(),
                Substitute.For<INavigationService<MainWindowViewModel>>(),
                Substitute.For<INavigationService<SettingsViewModel>>()
            )
            {
                SelectedProviderId = BananaVisionProviderIds.Gemini3Pro,
            };
            Page = new BananaVisionPage { DataContext = Vm };
        }

        public BananaVisionPageViewModel Vm { get; }

        public BananaVisionPage Page { get; }

        public bool FailRetry { get; set; }

        public ImageGenerationConversation AddConversation(
            string title,
            int minutesAgo,
            params string[] turns
        )
        {
            var conversation = new ImageGenerationConversation
            {
                Title = title,
                ProviderId = BananaVisionProviderIds.Gemini3Pro,
                UpdatedAt = DateTime.UtcNow.AddMinutes(-minutesAgo),
            };
            conversations.Add(conversation);
            messages[conversation.Id] = [];
            AddMessages(conversation, turns);
            return conversation;
        }

        public void AddMessages(ImageGenerationConversation conversation, params string[] turns)
        {
            for (var i = 0; i < turns.Length; i++)
            {
                Save(conversation.Id, i % 2 == 0 ? MessageRole.User : MessageRole.Assistant, turns[i]);
            }
        }

        /// <summary>
        /// Makes Send save the prompt and then wait; completing the returned source finishes it,
        /// with a reply or by throwing <paramref name="failure"/>.
        /// </summary>
        public TaskCompletionSource HoldSend(Exception? failure)
        {
            var gate = new TaskCompletionSource();
            chatService
                .SendMessageAsync(
                    Arg.Any<Guid>(),
                    Arg.Any<string>(),
                    Arg.Any<string?>(),
                    Arg.Any<List<string>?>(),
                    Arg.Any<Dictionary<string, object>?>(),
                    Arg.Any<IProgress<ImageGenerationProgress>?>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(async ci =>
                {
                    var id = ci.ArgAt<Guid>(0);
                    var user = Save(id, MessageRole.User, ci.ArgAt<string?>(2));
                    await gate.Task;
                    Touch(id);
                    if (failure != null)
                        throw failure;
                    return (user, (ImageGenerationMessage?)Save(id, MessageRole.Assistant, "sent reply"));
                });
            return gate;
        }

        public async Task OpenAsync(ImageGenerationConversation conversation)
        {
            window = new Window
            {
                Width = 1400,
                Height = 900,
                Content = Page,
            };
            window.Show();
            foreach (var c in await chatService.GetConversationsAsync())
            {
                Vm.Conversations.Add(c);
            }
            await SelectAsync(conversation);
        }

        /// <summary>Selects the conversation the way the sidebar does.</summary>
        public async Task SelectAsync(ImageGenerationConversation conversation)
        {
            Vm.CurrentConversation = Vm.Conversations.First(c => c.Id == conversation.Id);
            await Settle();
        }

        public string[] Texts() => Vm.Messages.OfType<TextMessage>().Select(m => m.Text).ToArray();

        public void Close() => window?.Close();

        private ImageGenerationMessage Save(Guid conversationId, MessageRole role, string? text)
        {
            var message = new ImageGenerationMessage
            {
                ConversationId = conversationId,
                Role = role,
                TextContent = text,
                Timestamp = DateTime.UtcNow.AddTicks(messages[conversationId].Count),
            };
            messages[conversationId].Add(message);
            return message;
        }

        /// <summary>Bumps the conversation to the top of the list, as a generation does.</summary>
        private void Touch(Guid conversationId) =>
            conversations.First(c => c.Id == conversationId).UpdatedAt = DateTime.UtcNow;
    }
}
