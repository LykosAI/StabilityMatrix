using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive.Linq;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(BananaVisionPage))]
[RegisterSingleton<BananaVisionPageViewModel>]
public partial class BananaVisionPageViewModel : PageViewModelBase
{
    private readonly ILogger<BananaVisionPageViewModel> logger;
    private readonly IImageGenerationChatService chatService;
    private readonly ISecretsManager secretsManager;
    private readonly INotificationService notificationService;
    private readonly RunningPackageService runningPackageService;
    private readonly IServiceManager<ViewModelBase> vmFactory;
    private readonly IModelIndexService modelIndexService;

    public override string Title => "BananaVision";
    public override IconSource IconSource =>
        new FASymbolIconSource { Symbol = "fa-solid fa-wand-magic-sparkles" };

    public IInferenceClientManager ClientManager { get; }

    [ObservableProperty]
    public partial string? NewMessageText { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    public partial bool IsGenerating { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial ImageGenerationConversation? CurrentConversation { get; set; }

    partial void OnCurrentConversationChanged(
        ImageGenerationConversation? oldValue,
        ImageGenerationConversation? newValue
    )
    {
        // Cancel any pending message load from the previous conversation
        loadMessagesCts?.Cancel();
        loadMessagesCts?.Dispose();
        loadMessagesCts = null;

        if (newValue != null)
        {
            logger.LogInformation(
                "Current conversation changed to: {ConversationId} - {Title} (provider: {ProviderId})",
                newValue.Id,
                newValue.Title,
                newValue.ProviderId
            );

            // Auto-switch to the conversation's last-used provider for convenience.
            // Users can still freely change it afterwards, and that change will be
            // remembered when they send the next message.
            if (newValue.ProviderId != SelectedProviderId)
            {
                SelectedProviderId = newValue.ProviderId;
            }

            // Create new cancellation token for this load operation
            loadMessagesCts = new CancellationTokenSource();
            var token = loadMessagesCts.Token;

            // Load messages for the new conversation (fire and forget with error handling)
            LoadMessagesForConversationAsync(newValue, token)
                .SafeFireAndForget(ex =>
                {
                    logger.LogError(
                        ex,
                        "Unhandled error loading messages for conversation {Id}",
                        newValue.Id
                    );
                });
        }
        else
        {
            logger.LogWarning("Current conversation set to null");
            Messages.Clear();
        }
    }

    /// <summary>
    /// Loads messages for a conversation without changing CurrentConversation
    /// </summary>
    private async Task LoadMessagesForConversationAsync(
        ImageGenerationConversation conversation,
        CancellationToken cancellationToken = default
    )
    {
        // Clear on UI thread
        await Dispatcher.UIThread.InvokeAsync(() => Messages.Clear());

        try
        {
            var messages = await chatService.GetMessagesAsync(conversation.Id);

            // Check if cancelled before updating UI (user may have switched conversations)
            cancellationToken.ThrowIfCancellationRequested();

            logger.LogInformation(
                "Loaded {Count} messages for conversation {Id}",
                messages.Count,
                conversation.Id
            );

            // Update UI on the UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var message in messages)
                {
                    // Show thinking content first (for assistant messages)
                    if (
                        message.Role == MessageRole.Assistant
                        && ShowThinkingOutput
                        && !string.IsNullOrEmpty(message.ThinkingContent)
                    )
                    {
                        Messages.Add(new ThinkingMessage(message.ThinkingContent));
                    }

                    if (!string.IsNullOrEmpty(message.TextContent))
                    {
                        Messages.Add(new TextMessage(message.TextContent, message.Role == MessageRole.User));
                    }

                    if (!string.IsNullOrEmpty(message.ImagePath) && File.Exists(message.ImagePath))
                    {
                        var bitmap = new Bitmap(message.ImagePath);
                        Messages.Add(new ImageMessage(bitmap, message.Role == MessageRole.User));
                    }
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Conversation switch cancelled this load - this is expected, don't log as error
            logger.LogDebug("Message loading cancelled for conversation {ConversationId}", conversation.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load messages for conversation {ConversationId}", conversation.Id);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ErrorMessage = $"Failed to load messages: {ex.Message}";
            });
        }
    }

    [ObservableProperty]
    public partial string? SelectedProviderId { get; set; }

    [ObservableProperty]
    public partial string? ProviderStatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsFluxKontextAvailable { get; set; }

    [ObservableProperty]
    public partial bool CanRetryLastMessage { get; set; }

    /// <summary>
    /// Whether to show thinking/reasoning output from Gemini 3 Pro
    /// </summary>
    [ObservableProperty]
    public partial bool ShowThinkingOutput { get; set; } = true;

    /// <summary>
    /// Whether the selected provider supports thinking output
    /// </summary>
    public bool SupportsThinking => BananaVisionProviderIds.SupportsThinking(SelectedProviderId);

    /// <summary>
    /// Whether the selected provider requires a local backend (ComfyUI)
    /// </summary>
    public bool RequiresLocalBackend => BananaVisionProviderIds.IsLocalProvider(SelectedProviderId);

    /// <summary>
    /// Whether the selected provider is a cloud/API provider (Gemini)
    /// </summary>
    public bool IsCloudProvider => BananaVisionProviderIds.IsCloudProvider(SelectedProviderId);

    /// <summary>
    /// Whether to show the Flux Kontext settings panel
    /// </summary>
    public bool ShowFluxSettings => SelectedProviderId == BananaVisionProviderIds.FluxKontext;

    /// <summary>
    /// Whether to show the Qwen Image Edit settings panel
    /// </summary>
    public bool ShowQwenSettings => SelectedProviderId == BananaVisionProviderIds.QwenImageEdit;

    /// <summary>
    /// Whether the Flux settings panel is expanded
    /// </summary>
    [ObservableProperty]
    public partial bool IsFluxSettingsExpanded { get; set; } = true;

    /// <summary>
    /// Whether the Qwen settings panel is expanded
    /// </summary>
    [ObservableProperty]
    public partial bool IsQwenSettingsExpanded { get; set; } = true;

    /// <summary>
    /// Selected Flux Kontext model
    /// </summary>
    [ObservableProperty]
    public partial HybridModelFile? SelectedFluxModel { get; set; }

    /// <summary>
    /// Selected Qwen Image Edit model
    /// </summary>
    [ObservableProperty]
    public partial HybridModelFile? SelectedQwenModel { get; set; }

    /// <summary>
    /// Available Flux Kontext models (filtered by BaseModel metadata or untagged)
    /// </summary>
    public ObservableCollection<HybridModelFile> AvailableFluxModels { get; } = [];

    /// <summary>
    /// Available Qwen Image Edit models (filtered by BaseModel metadata or filename)
    /// </summary>
    public ObservableCollection<HybridModelFile> AvailableQwenModels { get; } = [];

    /// <summary>
    /// Available LoRA models for Flux Kontext
    /// </summary>
    public ObservableCollection<HybridModelFile> AvailableFluxLoras { get; } = [];

    /// <summary>
    /// Available LoRA models for Qwen Image Edit
    /// </summary>
    public ObservableCollection<HybridModelFile> AvailableQwenLoras { get; } = [];

    /// <summary>
    /// Selected LoRAs with weights
    /// </summary>
    public ObservableCollection<SelectedLora> SelectedLoras { get; } = [];

    /// <summary>
    /// Available aspect ratio presets
    /// </summary>
    public ObservableCollection<AspectRatioOption> AvailableAspectRatios { get; } =
        [
            new AspectRatioOption("1:1", "Square", 1024, 1024),
            new AspectRatioOption("16:9", "Landscape Wide", 1344, 768),
            new AspectRatioOption("9:16", "Portrait Tall", 768, 1344),
            new AspectRatioOption("4:3", "Landscape", 1152, 896),
            new AspectRatioOption("3:4", "Portrait", 896, 1152),
            new AspectRatioOption("3:2", "Photo Landscape", 1216, 832),
            new AspectRatioOption("2:3", "Photo Portrait", 832, 1216),
            new AspectRatioOption("21:9", "Ultrawide", 1536, 640),
            new AspectRatioOption("9:21", "Ultra Tall", 640, 1536),
        ];

    /// <summary>
    /// Selected aspect ratio
    /// </summary>
    [ObservableProperty]
    public partial AspectRatioOption? SelectedAspectRatio { get; set; }

    /// <summary>
    /// Whether to use custom resolution instead of aspect ratio presets
    /// </summary>
    [ObservableProperty]
    public partial bool UseCustomResolution { get; set; }

    /// <summary>
    /// Custom width when UseCustomResolution is true
    /// </summary>
    [ObservableProperty]
    public partial int CustomWidth { get; set; } = 1024;

    /// <summary>
    /// Custom height when UseCustomResolution is true
    /// </summary>
    [ObservableProperty]
    public partial int CustomHeight { get; set; } = 1024;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComfyRunning))]
    public partial PackagePair? RunningPackage { get; set; }

    [ObservableProperty]
    public partial bool IsWaitingForConnection { get; set; }

    /// <summary>
    /// Indicates whether the user is dragging an image over the page
    /// </summary>
    [ObservableProperty]
    public partial bool IsDragOverImage { get; set; }

    partial void OnIsWaitingForConnectionChanged(bool value)
    {
        UpdateProviderStatus();
    }

    public bool IsComfyRunning => RunningPackage?.BasePackage is ComfyUI;

    private string? lastMessageText;
    private List<string>? lastMessageImagePaths;
    private IDisposable? startupCompleteSubscription;
    private bool hasShownMissingModelsDialog;
    private CancellationTokenSource? loadMessagesCts;

    /// <summary>
    /// Messages in the current conversation. Can contain MessageBase or ThinkingMessage.
    /// </summary>
    public ObservableCollection<object> Messages { get; }

    /// <summary>
    /// Event raised when the message list should scroll to the end
    /// </summary>
    public event EventHandler? ScrollToEndRequested;

    public ObservableCollection<ImageGenerationConversation> Conversations { get; set; } = [];
    public ObservableCollection<ProviderDisplayItem> AvailableProviders { get; set; } = [];

    /// <summary>
    /// Pending images to be sent with the next message
    /// </summary>
    public ObservableCollection<PendingImage> PendingImages { get; set; } = [];

    // Will be set by the view
    public IStorageProvider? StorageProvider { get; set; }

    public BananaVisionPageViewModel(
        ILogger<BananaVisionPageViewModel> logger,
        IImageGenerationChatService chatService,
        ISecretsManager secretsManager,
        INotificationService notificationService,
        IInferenceClientManager inferenceClientManager,
        RunningPackageService runningPackageService,
        IServiceManager<ViewModelBase> vmFactory,
        IModelIndexService modelIndexService
    )
    {
        this.logger = logger;
        this.chatService = chatService;
        this.secretsManager = secretsManager;
        this.notificationService = notificationService;
        this.runningPackageService = runningPackageService;
        this.vmFactory = vmFactory;
        this.modelIndexService = modelIndexService;

        ClientManager = inferenceClientManager;

        // Initialize Messages collection and subscribe to changes for auto-scroll
        Messages = [];
        Messages.CollectionChanged += OnMessagesCollectionChanged;

        // Load available providers
        var providers = chatService.GetAvailableProviders();
        foreach (var provider in providers)
        {
            AvailableProviders.Add(new ProviderDisplayItem(provider.ProviderId, provider.ProviderName));
        }

        // Set default provider (use the first provider's ID)
        SelectedProviderId = AvailableProviders.FirstOrDefault()?.Id;

        // Set default aspect ratio (1:1 Square)
        SelectedAspectRatio = AvailableAspectRatios.FirstOrDefault();

        // Subscribe to connection status changes
        ClientManager.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != nameof(IInferenceClientManager.IsConnected))
                return;

            UpdateProviderStatus();

            // When connected and using a local provider, check for missing models
            if (ClientManager.IsConnected && RequiresLocalBackend)
            {
                CheckAndShowMissingModelsDialogAsync()
                    .SafeFireAndForget(ex =>
                    {
                        logger.LogError(ex, "Failed to check for missing models");
                    });
            }
        };

        // Subscribe to running package changes
        runningPackageService.RunningPackages.CollectionChanged += (s, e) =>
        {
            // ComfyZluda inherits from ComfyUI, so this check covers both
            var comfyPackage = runningPackageService
                .RunningPackages.FirstOrDefault(p => p.Value.RunningPackage.BasePackage is ComfyUI)
                .Value?.RunningPackage;

            // Handle package startup - auto-connect when ComfyUI starts
            if (comfyPackage != null && RunningPackage == null)
            {
                RunningPackage = comfyPackage;

                // Dispose previous subscription if any
                startupCompleteSubscription?.Dispose();

                // Subscribe to StartupComplete event for auto-connect
                IsWaitingForConnection = true;
                startupCompleteSubscription = Observable
                    .FromEventPattern<string>(
                        comfyPackage.BasePackage,
                        nameof(comfyPackage.BasePackage.StartupComplete)
                    )
                    .Take(1)
                    .Subscribe(_ =>
                    {
                        Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            // Only auto-connect for local providers (Flux Kontext, Qwen Image Edit, etc.)
                            if (RequiresLocalBackend && ClientManager.CanUserConnect)
                            {
                                logger.LogInformation(
                                    "ComfyUI startup complete, auto-connecting for local provider..."
                                );
                                await ConnectAsync();
                            }

                            IsWaitingForConnection = false;
                        });
                    });
            }
            else if (comfyPackage == null && RunningPackage != null)
            {
                // Package stopped
                startupCompleteSubscription?.Dispose();
                startupCompleteSubscription = null;
                IsWaitingForConnection = false;
            }

            RunningPackage = comfyPackage;
            UpdateProviderStatus();
        };

        // Initial status update
        var initialComfyPackage = runningPackageService
            .RunningPackages.FirstOrDefault(p => p.Value.RunningPackage.BasePackage is ComfyUI)
            .Value?.RunningPackage;

        RunningPackage = initialComfyPackage;

        // If ComfyUI is already running and we're using a local provider, try to connect
        if (initialComfyPackage != null && RequiresLocalBackend && !ClientManager.IsConnected)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(500); // Small delay to ensure ComfyUI is ready
                if (ClientManager.CanUserConnect)
                {
                    logger.LogInformation("ComfyUI already running on load, attempting connection...");
                    await ConnectAsync();
                }
            });
        }

        UpdateProviderStatus();
    }

    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        logger.LogInformation("BananaVisionPage loaded, initializing...");

        // Load conversations
        logger.LogInformation("Loading conversations from database...");
        await LoadConversationsAsync();
        logger.LogInformation("Loaded {Count} conversations", Conversations.Count);

        // Create or load a conversation
        if (Conversations.Count == 0 && SelectedProviderId != null)
        {
            logger.LogInformation("No conversations found, creating new conversation");
            await NewConversationAsync();
        }
        else if (Conversations.Count > 0)
        {
            logger.LogInformation("Loading most recent conversation: {ConversationId}", Conversations[0].Id);
            await LoadConversationAsync(Conversations[0]);
        }
    }

    private async Task LoadConversationsAsync()
    {
        try
        {
            var conversations = await chatService.GetConversationsAsync();

            // Update UI on the UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Conversations.Clear();
                foreach (var conversation in conversations)
                {
                    Conversations.Add(conversation);
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load conversations");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ErrorMessage = $"Failed to load conversations: {ex.Message}";
            });
        }
    }

    [RelayCommand]
    private async Task NewConversationAsync()
    {
        if (string.IsNullOrEmpty(SelectedProviderId))
        {
            notificationService.Show("Error", "Please select a provider", NotificationType.Error);
            return;
        }

        try
        {
            var conversation = await chatService.CreateConversationAsync(SelectedProviderId);
            Conversations.Insert(0, conversation);
            await LoadConversationAsync(conversation);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create conversation");
            notificationService.Show(
                "Error",
                $"Failed to create conversation: {ex.Message}",
                NotificationType.Error
            );
        }
    }

    [RelayCommand]
    private Task LoadConversationAsync(ImageGenerationConversation conversation)
    {
        // Setting CurrentConversation triggers OnCurrentConversationChanged which loads the messages
        CurrentConversation = conversation;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task DeleteConversationAsync(ImageGenerationConversation conversation)
    {
        // Show confirmation dialog
        var dialog = new ContentDialog
        {
            Title = "Delete Conversation",
            Content =
                $"Are you sure you want to delete \"{conversation.Title}\"?\n\nThis will also delete all messages and generated images in this conversation.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await chatService.DeleteConversationAsync(conversation.Id);
            Conversations.Remove(conversation);

            if (CurrentConversation?.Id == conversation.Id)
            {
                Messages.Clear();
                CurrentConversation = null;

                // Load first conversation if available
                if (Conversations.Count > 0)
                {
                    await LoadConversationAsync(Conversations[0]);
                }
            }

            notificationService.Show("Success", "Conversation deleted", NotificationType.Success);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete conversation {ConversationId}", conversation.Id);
            notificationService.Show(
                "Error",
                $"Failed to delete conversation: {ex.Message}",
                NotificationType.Error
            );
        }
    }

    [RelayCommand]
    private async Task RenameConversationAsync(ImageGenerationConversation conversation)
    {
        try
        {
            var textBox = new TextBox
            {
                Text = conversation.Title,
                Watermark = "Enter conversation name...",
                MinWidth = 300,
            };

            var dialog = new ContentDialog
            {
                Title = "Rename Conversation",
                Content = textBox,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                conversation.Title = textBox.Text.Trim();
                await chatService.UpdateConversationAsync(conversation);

                // Refresh the list to update UI
                var index = Conversations.IndexOf(conversation);
                if (index >= 0)
                {
                    Conversations[index] = conversation;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to rename conversation {ConversationId}", conversation.Id);
            notificationService.Show(
                "Error",
                $"Failed to rename conversation: {ex.Message}",
                NotificationType.Error
            );
        }
    }

    /// <summary>
    /// Gets the display name for a provider ID
    /// </summary>
    public string GetProviderDisplayName(string? providerId)
    {
        if (string.IsNullOrEmpty(providerId))
            return "Unknown";
        return AvailableProviders.FirstOrDefault(p => p.Id == providerId)?.DisplayName ?? providerId;
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            logger.LogInformation("Attempting to connect to ComfyUI...");
            await ClientManager.ConnectAsync();
            notificationService.Show(
                "Connected",
                "Successfully connected to ComfyUI",
                NotificationType.Success
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to ComfyUI");
            notificationService.Show("Connection Failed", ex.Message, NotificationType.Error);
        }
    }

    [RelayCommand]
    private async Task ShowConnectionHelpAsync()
    {
        var viewModel = App.Services.GetRequiredService<InferenceConnectionHelpViewModel>();
        var dialog = viewModel.CreateDialog();

        await dialog.ShowAsync();

        // After dialog closes, check if we should connect
        if (IsComfyRunning && ClientManager.CanUserConnect)
        {
            await ConnectAsync();
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task SendMessageAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(NewMessageText) && PendingImages.Count == 0)
            return;

        if (CurrentConversation == null)
        {
            notificationService.Show("Error", "No conversation selected", NotificationType.Error);
            return;
        }

        if (string.IsNullOrEmpty(SelectedProviderId))
        {
            notificationService.Show("Error", "Please select a provider", NotificationType.Error);
            return;
        }

        // Check API key only for Gemini provider
        if (BananaVisionProviderIds.IsCloudProvider(SelectedProviderId))
        {
            var secrets = await secretsManager.SafeLoadAsync();
            if (string.IsNullOrEmpty(secrets.GeminiApiKey))
            {
                ErrorMessage = "Gemini API key not configured. Please add it in Settings.";
                notificationService.Show(
                    "API Key Required",
                    "Please configure your Gemini API key in Settings.",
                    NotificationType.Warning
                );
                return;
            }
        }

        var messageText = NewMessageText;
        var imagePaths = PendingImages.Select(p => p.FilePath).ToList();

        // Store for retry
        lastMessageText = messageText;
        lastMessageImagePaths = imagePaths.Count > 0 ? imagePaths : null;

        NewMessageText = string.Empty;
        ErrorMessage = null;
        CanRetryLastMessage = false;

        // Add user message to UI immediately
        if (!string.IsNullOrWhiteSpace(messageText))
        {
            Messages.Add(new TextMessage(messageText, true));
        }

        // Show pending images in chat
        foreach (var pendingImage in PendingImages)
        {
            Messages.Add(new ImageMessage(pendingImage.Bitmap, true));
        }

        // Clear pending images
        PendingImages.Clear();

        IsGenerating = true;

        try
        {
            // Build provider options
            var providerOptions = BuildProviderOptions();

            var (userMessage, assistantMessage) = await chatService.SendMessageAsync(
                CurrentConversation.Id,
                SelectedProviderId!,
                messageText,
                imagePaths.Count > 0 ? imagePaths : null,
                providerOptions,
                cancellationToken
            );

            // Add assistant response to UI
            if (assistantMessage != null)
            {
                // Show thinking content first if available and user wants it
                if (ShowThinkingOutput && !string.IsNullOrEmpty(assistantMessage.ThinkingContent))
                {
                    Messages.Add(new ThinkingMessage(assistantMessage.ThinkingContent));
                }

                if (!string.IsNullOrEmpty(assistantMessage.TextContent))
                {
                    Messages.Add(new TextMessage(assistantMessage.TextContent, false));
                }

                if (
                    !string.IsNullOrEmpty(assistantMessage.ImagePath)
                    && File.Exists(assistantMessage.ImagePath)
                )
                {
                    var bitmap = new Bitmap(assistantMessage.ImagePath);
                    Messages.Add(new ImageMessage(bitmap, false));
                }
            }

            // Reload conversations to update timestamps and titles
            await LoadConversationsAsync();

            // Update current conversation reference to reflect title changes
            if (CurrentConversation != null)
            {
                var updatedConversation = Conversations.FirstOrDefault(c => c.Id == CurrentConversation.Id);
                if (updatedConversation != null)
                {
                    CurrentConversation = updatedConversation;
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Message generation cancelled");
            notificationService.Show("Cancelled", "Image generation cancelled", NotificationType.Information);
            CanRetryLastMessage = true; // Enable retry after cancel
        }
        catch (ImageGenerationException ex)
        {
            // Expected error from generation (provider error, API error, etc.)
            logger.LogWarning("Image generation failed: {Message}", ex.Message);
            ErrorMessage = ex.Message;
            notificationService.Show("Generation Failed", ex.Message, NotificationType.Warning);
            CanRetryLastMessage = true;
        }
        catch (Exception ex)
        {
            // Unexpected error
            logger.LogError(ex, "Unexpected error sending message");
            ErrorMessage = $"Unexpected error: {ex.Message}";
            notificationService.Show("Error", ex.Message, NotificationType.Error);
            CanRetryLastMessage = true;
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task RetryLastMessageAsync()
    {
        if (CurrentConversation == null)
            return;

        if (string.IsNullOrEmpty(SelectedProviderId))
        {
            notificationService.Show("Error", "Please select a provider", NotificationType.Error);
            return;
        }

        // Clear error state
        ErrorMessage = null;
        CanRetryLastMessage = false;
        IsGenerating = true;

        try
        {
            // Build provider options
            var providerOptions = BuildProviderOptions();

            // Retry generation - this doesn't create a new user message
            var assistantMessage = await chatService.RetryGenerationAsync(
                CurrentConversation.Id,
                SelectedProviderId,
                providerOptions,
                CancellationToken.None
            );

            // Add only the assistant response to UI
            if (ShowThinkingOutput && !string.IsNullOrEmpty(assistantMessage.ThinkingContent))
            {
                Messages.Add(new ThinkingMessage(assistantMessage.ThinkingContent));
            }

            if (!string.IsNullOrEmpty(assistantMessage.TextContent))
            {
                Messages.Add(new TextMessage(assistantMessage.TextContent, false));
            }

            if (!string.IsNullOrEmpty(assistantMessage.ImagePath) && File.Exists(assistantMessage.ImagePath))
            {
                var bitmap = new Bitmap(assistantMessage.ImagePath);
                Messages.Add(new ImageMessage(bitmap, false));
            }

            // Reload conversations to update timestamps
            await LoadConversationsAsync();
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Retry generation cancelled");
            notificationService.Show("Cancelled", "Image generation cancelled", NotificationType.Information);
            CanRetryLastMessage = true;
        }
        catch (ImageGenerationException ex)
        {
            logger.LogWarning("Retry generation failed: {Message}", ex.Message);
            ErrorMessage = ex.Message;
            notificationService.Show("Generation Failed", ex.Message, NotificationType.Warning);
            CanRetryLastMessage = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during retry");
            ErrorMessage = $"Unexpected error: {ex.Message}";
            notificationService.Show("Error", ex.Message, NotificationType.Error);
            CanRetryLastMessage = true;
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private void DismissError()
    {
        ErrorMessage = null;
        CanRetryLastMessage = false;
    }

    /// <summary>
    /// Builds the provider options dictionary based on current settings
    /// </summary>
    private Dictionary<string, object> BuildProviderOptions()
    {
        Dictionary<string, object>? providerOptions = null;

        if (SupportsThinking && ShowThinkingOutput)
        {
            providerOptions = new Dictionary<string, object>
            {
                ["enableThinking"] = true,
                ["thinkingBudget"] = 2048,
            };
        }

        if (SelectedProviderId == BananaVisionProviderIds.FluxKontext)
        {
            providerOptions ??= new Dictionary<string, object>();
            if (SelectedFluxModel != null)
                providerOptions["CustomUnetModel"] = SelectedFluxModel;
            if (SelectedLoras.Count > 0)
                providerOptions["SelectedLoras"] = SelectedLoras.ToList();
        }

        if (SelectedProviderId == BananaVisionProviderIds.QwenImageEdit)
        {
            providerOptions ??= new Dictionary<string, object>();
            if (SelectedQwenModel != null)
                providerOptions["CustomUnetModel"] = SelectedQwenModel;
            if (SelectedLoras.Count > 0)
                providerOptions["SelectedLoras"] = SelectedLoras.ToList();
        }

        providerOptions ??= new Dictionary<string, object>();

        if (UseCustomResolution)
        {
            providerOptions["Width"] = CustomWidth;
            providerOptions["Height"] = CustomHeight;
        }
        else if (SelectedAspectRatio != null)
        {
            providerOptions["aspectRatio"] = SelectedAspectRatio.Ratio;
            providerOptions["Width"] = SelectedAspectRatio.Width;
            providerOptions["Height"] = SelectedAspectRatio.Height;
        }

        return providerOptions;
    }

    /// <summary>
    /// Handles key down events from the message input TextBox.
    /// Enter sends the message, Shift+Enter adds a new line.
    /// </summary>
    [RelayCommand]
    private void TextBoxKeyDown(KeyEventArgs? e)
    {
        if (e?.Key != Key.Enter)
            return;

        // Shift+Enter = let TextBox handle it naturally (insert newline at cursor position)
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            // Don't handle it - let the TextBox process the newline naturally
            return;
        }

        // Plain Enter = send message (but only if not already generating)
        if (!IsGenerating && SendMessageCommand.CanExecute(null))
        {
            e.Handled = true;
            SendMessageCommand.Execute(null);
        }
        else
        {
            // Prevent the Enter from doing anything if we're generating
            e.Handled = true;
        }
    }

    [RelayCommand]
    private async Task AddImageAsync()
    {
        if (StorageProvider == null)
        {
            notificationService.Show("Error", "Storage provider not available", NotificationType.Error);
            return;
        }

        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Select Images",
                    AllowMultiple = true,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("Images")
                        {
                            Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif"],
                        },
                    ],
                }
            );

            if (files.Count == 0)
                return;

            foreach (var file in files)
            {
                var imagePath = file.Path.LocalPath;
                var bitmap = new Bitmap(imagePath);

                PendingImages.Add(new PendingImage { FilePath = imagePath, Bitmap = bitmap });
            }

            notificationService.Show(
                "Images Added",
                $"Added {files.Count} image(s). They will be sent with your next message.",
                NotificationType.Success
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add images");
            notificationService.Show("Error", $"Failed to add images: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private void RemovePendingImage(PendingImage image)
    {
        PendingImages.Remove(image);
        image.Dispose();
    }

    /// <summary>
    /// Adds images from file paths (used by drag and drop)
    /// </summary>
    public async Task AddImagesFromPathsAsync(IEnumerable<string> imagePaths)
    {
        try
        {
            var pathsList = imagePaths.ToList();
            var addedCount = 0;

            foreach (var imagePath in pathsList)
            {
                if (!File.Exists(imagePath))
                    continue;

                var bitmap = new Bitmap(imagePath);
                PendingImages.Add(new PendingImage { FilePath = imagePath, Bitmap = bitmap });
                addedCount++;
            }

            if (addedCount > 0)
            {
                notificationService.Show(
                    "Images Added",
                    $"Added {addedCount} image(s). They will be sent with your next message.",
                    NotificationType.Success
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add images from drag and drop");
            notificationService.Show("Error", $"Failed to add images: {ex.Message}", NotificationType.Error);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Supported image extensions for clipboard paste
    /// </summary>
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".gif",
        ".bmp",
    };

    /// <summary>
    /// Tries to paste images from the clipboard. Returns true if images were pasted.
    /// </summary>
    public async Task<bool> TryPasteImagesFromClipboardAsync()
    {
        try
        {
            var clipboard = App.Clipboard;
            if (clipboard == null)
                return false;

            // First, check for files in clipboard (e.g., copied from file explorer)
            var formats = await clipboard.GetFormatsAsync();

            if (formats.Contains(DataFormats.Files))
            {
                var data = await clipboard.GetDataAsync(DataFormats.Files);
                if (data is IEnumerable<IStorageItem> files)
                {
                    var imagePaths = files
                        .Select(f => f.Path.LocalPath)
                        .Where(p => SupportedImageExtensions.Contains(Path.GetExtension(p)))
                        .ToList();

                    if (imagePaths.Count > 0)
                    {
                        await AddImagesFromPathsAsync(imagePaths);
                        return true;
                    }
                }
            }

            // Check for bitmap/image data in clipboard (e.g., screenshots, copied images)
            // Try common image formats
            foreach (
                var format in new[] { "PNG", "image/png", "Bitmap", "DeviceIndependentBitmap", "image/bmp" }
            )
            {
                if (!formats.Contains(format))
                    continue;

                var data = await clipboard.GetDataAsync(format);
                if (data is byte[] imageBytes && imageBytes.Length > 0)
                {
                    var tempPath = await SaveClipboardImageToTempFileAsync(imageBytes, format);
                    if (tempPath != null)
                    {
                        await AddImagesFromPathsAsync([tempPath]);
                        return true;
                    }
                }
                else if (data is Stream stream)
                {
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    var bytes = ms.ToArray();

                    if (bytes.Length > 0)
                    {
                        var tempPath = await SaveClipboardImageToTempFileAsync(bytes, format);
                        if (tempPath != null)
                        {
                            await AddImagesFromPathsAsync([tempPath]);
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to paste images from clipboard");
            return false;
        }
    }

    /// <summary>
    /// Saves clipboard image bytes to a temporary file
    /// </summary>
    private async Task<string?> SaveClipboardImageToTempFileAsync(byte[] imageBytes, string format)
    {
        try
        {
            var extension = format.ToLowerInvariant() switch
            {
                "png" or "image/png" => ".png",
                "image/jpeg" or "jpeg" or "jpg" => ".jpg",
                "image/bmp" or "bitmap" or "deviceindependentbitmap" => ".bmp",
                _ => ".png",
            };

            var tempDir = Path.Combine(Path.GetTempPath(), "StabilityMatrix", "ClipboardImages");
            Directory.CreateDirectory(tempDir);

            var shortGuid = Guid.NewGuid().ToString("N")[..8];
            var fileName = $"clipboard_{DateTime.Now:yyyyMMdd_HHmmss}_{shortGuid}{extension}";
            var tempPath = Path.Combine(tempDir, fileName);

            await File.WriteAllBytesAsync(tempPath, imageBytes);

            logger.LogInformation("Saved clipboard image to temp file: {Path}", tempPath);
            return tempPath;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save clipboard image to temp file");
            return null;
        }
    }

    [RelayCommand]
    private void ClearPendingImages()
    {
        foreach (var image in PendingImages)
        {
            image.Dispose();
        }
        PendingImages.Clear();
    }

    [RelayCommand]
    private async Task EditPendingImageAsync(PendingImage image)
    {
        try
        {
            var editorVm = vmFactory.Get<ImageAnnotationEditorViewModel>();
            editorVm.LoadImage(image.Bitmap, image.FilePath);

            var dialog = editorVm.GetDialog();
            var result = await dialog.ShowAsync();

            if (result == FluentAvalonia.UI.Controls.ContentDialogResult.Primary && editorVm.HasAnnotations)
            {
                // Save the annotated image to a temp file
                var annotatedPath = await editorVm.SaveAnnotatedImageAsync();

                if (annotatedPath != null)
                {
                    // Replace the pending image with the annotated version
                    var index = PendingImages.IndexOf(image);
                    if (index >= 0)
                    {
                        var annotatedBitmap = new Bitmap(annotatedPath);
                        var oldImage = PendingImages[index];
                        PendingImages[index] = new PendingImage
                        {
                            FilePath = annotatedPath,
                            Bitmap = annotatedBitmap,
                        };
                        oldImage.Dispose(); // Dispose the old bitmap

                        notificationService.Show(
                            "Image Updated",
                            "Your annotations have been applied to the image.",
                            NotificationType.Success
                        );
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to edit image");
            notificationService.Show("Error", $"Failed to edit image: {ex.Message}", NotificationType.Error);
        }
    }

    /// <summary>
    /// Preview an image in a full-size dialog
    /// </summary>
    [RelayCommand]
    private async Task PreviewImageAsync(Bitmap? bitmap)
    {
        if (bitmap == null)
            return;

        try
        {
            var viewerVm = vmFactory.Get<ImageViewerViewModel>();
            viewerVm.ImageSource = new ImageSource(bitmap);

            var dialog = viewerVm.GetDialog();
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to preview image");
        }
    }

    partial void OnSelectedProviderIdChanged(string? value)
    {
        // Log provider change - the actual conversation update happens when sending a message
        if (CurrentConversation != null && value != null && value != CurrentConversation.ProviderId)
        {
            logger.LogInformation(
                "Provider selection changed from {OldProvider} to {NewProvider} for conversation {ConversationId}",
                CurrentConversation.ProviderId,
                value,
                CurrentConversation.Id
            );
        }

        // If switching away from local providers, clean up any pending connection
        if (!BananaVisionProviderIds.IsLocalProvider(value))
        {
            startupCompleteSubscription?.Dispose();
            startupCompleteSubscription = null;
            IsWaitingForConnection = false;
            hasShownMissingModelsDialog = false; // Reset for next time
        }

        // Update provider status for the new provider
        UpdateProviderStatus();

        // Notify that provider-related properties may have changed
        OnPropertyChanged(nameof(SupportsThinking));
        OnPropertyChanged(nameof(RequiresLocalBackend));
        OnPropertyChanged(nameof(IsCloudProvider));
        OnPropertyChanged(nameof(ShowFluxSettings));
        OnPropertyChanged(nameof(ShowQwenSettings));

        // Load available Flux models when switching to Flux Kontext
        if (value == BananaVisionProviderIds.FluxKontext)
        {
            LoadAvailableFluxModels();

            // Auto-show missing models dialog if connected and models are missing
            CheckAndShowMissingModelsDialogAsync()
                .SafeFireAndForget(ex =>
                {
                    logger.LogError(ex, "Failed to check for missing Flux models");
                });
        }

        // Load available Qwen models when switching to Qwen Image Edit
        if (value == BananaVisionProviderIds.QwenImageEdit)
        {
            LoadAvailableQwenModels();

            // Auto-show missing models dialog if connected and models are missing
            CheckAndShowMissingModelsDialogAsync()
                .SafeFireAndForget(ex =>
                {
                    logger.LogError(ex, "Failed to check for missing Qwen models");
                });
        }
    }

    private void UpdateProviderStatus()
    {
        // Check if this is a local provider with model requirements
        var modelManager = LocalProviderModelManagerRegistry.GetManager(SelectedProviderId);

        if (modelManager != null)
        {
            // This is a local provider - check ComfyUI and model status

            // Check if ComfyUI is running
            if (!IsComfyRunning)
            {
                ProviderStatusMessage = "⚠️ ComfyUI is not running. Click Launch to start.";
                IsFluxKontextAvailable = false;
                HasMissingModels = false;
                return;
            }

            // Check if we're waiting for connection
            if (IsWaitingForConnection)
            {
                ProviderStatusMessage = "🔄 Connecting to ComfyUI...";
                IsFluxKontextAvailable = false;
                HasMissingModels = false;
                return;
            }

            // Check ComfyUI connection status
            if (!ClientManager.IsConnected)
            {
                ProviderStatusMessage = "⚠️ Not connected to ComfyUI. Click Connect.";
                IsFluxKontextAvailable = false;
                HasMissingModels = false;
                return;
            }

            // Check if required models are available
            if (!modelManager.AreModelsAvailable(ClientManager))
            {
                var missingModelNames = modelManager.GetMissingModelNames(ClientManager).ToList();
                var modelsList = string.Join(", ", missingModelNames);
                ProviderStatusMessage = $"⚠️ Missing: {modelsList}";
                IsFluxKontextAvailable = false;
                HasMissingModels = true;
                return;
            }

            // All good
            ProviderStatusMessage = $"✅ {modelManager.ProviderDisplayName} is ready";
            IsFluxKontextAvailable = true;
            HasMissingModels = false;
        }
        else
        {
            // Cloud providers or providers without model requirements
            ProviderStatusMessage = null;
            IsFluxKontextAvailable = false;
            HasMissingModels = false;
        }
    }

    /// <summary>
    /// Handles collection changes to trigger auto-scroll
    /// </summary>
    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Request scroll to end when new messages are added
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
                },
                DispatcherPriority.Background
            );
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Cancel and dispose any pending message load
            loadMessagesCts?.Cancel();
            loadMessagesCts?.Dispose();
            loadMessagesCts = null;

            // Dispose startup subscription
            startupCompleteSubscription?.Dispose();
            startupCompleteSubscription = null;

            // Dispose pending images
            foreach (var image in PendingImages)
            {
                image.Dispose();
            }
            PendingImages.Clear();

            // Dispose message bitmaps
            foreach (var message in Messages)
            {
                if (message is ImageMessage imageMessage)
                {
                    imageMessage.Image?.Dispose();
                }
            }
            Messages.Clear();

            // Unsubscribe from collection changed
            Messages.CollectionChanged -= OnMessagesCollectionChanged;
        }

        base.Dispose(disposing);
    }
}
