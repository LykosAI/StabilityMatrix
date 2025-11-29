using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
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
using StabilityMatrix.Core.Models.Progress;
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
    public override IconSource IconSource => new FASymbolIconSource { Symbol = "fa-solid fa-glasses" };

    public IInferenceClientManager ClientManager { get; }

    [ObservableProperty]
    public partial string? NewMessageText { get; set; }

    [ObservableProperty]
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
        if (newValue != null)
        {
            logger.LogInformation(
                "Current conversation changed to: {ConversationId} - {Title}",
                newValue.Id,
                newValue.Title
            );

            // Auto-switch provider to match the conversation's provider
            if (newValue.ProviderId != SelectedProviderId)
            {
                SelectedProviderId = newValue.ProviderId;
            }

            // Load messages for the new conversation (fire and forget with error handling)
            _ = LoadMessagesForConversationAsync(newValue);
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
    private async Task LoadMessagesForConversationAsync(ImageGenerationConversation conversation)
    {
        Messages.Clear();

        try
        {
            var messages = await chatService.GetMessagesAsync(conversation.Id);
            logger.LogInformation(
                "Loaded {Count} messages for conversation {Id}",
                messages.Count,
                conversation.Id
            );

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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load messages for conversation {ConversationId}", conversation.Id);
            ErrorMessage = $"Failed to load messages: {ex.Message}";
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
    public bool SupportsThinking => SelectedProviderId == "gemini-3-pro";

    /// <summary>
    /// Whether the selected provider requires a local backend (ComfyUI)
    /// </summary>
    public bool RequiresLocalBackend => SelectedProviderId is "flux-kontext" or "qwen-image-edit";

    /// <summary>
    /// Whether the selected provider is a cloud/API provider (Gemini)
    /// </summary>
    public bool IsCloudProvider => SelectedProviderId?.Contains("gemini") == true;

    /// <summary>
    /// Whether to show the Flux Kontext settings panel
    /// </summary>
    public bool ShowFluxSettings => SelectedProviderId == "flux-kontext";

    /// <summary>
    /// Whether to show the Qwen Image Edit settings panel
    /// </summary>
    public bool ShowQwenSettings => SelectedProviderId == "qwen-image-edit";

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

    partial void OnIsWaitingForConnectionChanged(bool value)
    {
        UpdateProviderStatus();
    }

    public bool IsComfyRunning => RunningPackage?.BasePackage is ComfyUI;

    private string? lastMessageText;
    private List<string>? lastMessageImagePaths;
    private IDisposable? startupCompleteSubscription;
    private bool hasShownMissingModelsDialog;

    /// <summary>
    /// Messages in the current conversation. Can contain MessageBase or ThinkingMessage.
    /// </summary>
    public ObservableCollection<object> Messages { get; set; } = [];
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
                _ = CheckAndShowMissingModelsDialogAsync();
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
            Conversations.Clear();
            foreach (var conversation in conversations)
            {
                Conversations.Add(conversation);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load conversations");
            ErrorMessage = $"Failed to load conversations: {ex.Message}";
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

        // Check API key only for Gemini provider
        if (SelectedProviderId?.Contains("gemini") == true)
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
            Dictionary<string, object>? providerOptions = null;

            // Add thinking support options for Gemini 3 Pro
            if (SupportsThinking && ShowThinkingOutput)
            {
                providerOptions = new Dictionary<string, object>
                {
                    ["enableThinking"] = true,
                    ["thinkingBudget"] = 2048,
                };
            }

            // Add Flux Kontext model and LoRA options
            if (SelectedProviderId == "flux-kontext")
            {
                providerOptions ??= new Dictionary<string, object>();

                if (SelectedFluxModel != null)
                {
                    providerOptions["CustomUnetModel"] = SelectedFluxModel;
                }

                if (SelectedLoras.Count > 0)
                {
                    providerOptions["SelectedLoras"] = SelectedLoras.ToList();
                }
            }

            // Add Qwen Image Edit model and LoRA options
            if (SelectedProviderId == "qwen-image-edit")
            {
                providerOptions ??= new Dictionary<string, object>();

                if (SelectedQwenModel != null)
                {
                    providerOptions["CustomUnetModel"] = SelectedQwenModel;
                }

                if (SelectedLoras.Count > 0)
                {
                    providerOptions["SelectedLoras"] = SelectedLoras.ToList();
                }
            }

            // Add aspect ratio / resolution options
            providerOptions ??= new Dictionary<string, object>();

            if (UseCustomResolution)
            {
                // For local providers, pass explicit width/height
                providerOptions["Width"] = CustomWidth;
                providerOptions["Height"] = CustomHeight;
            }
            else if (SelectedAspectRatio != null)
            {
                // For cloud providers (Gemini), pass aspect ratio string
                providerOptions["aspectRatio"] = SelectedAspectRatio.Ratio;

                // For local providers, also pass width/height
                providerOptions["Width"] = SelectedAspectRatio.Width;
                providerOptions["Height"] = SelectedAspectRatio.Height;
            }

            var (userMessage, assistantMessage) = await chatService.SendMessageAsync(
                CurrentConversation.Id,
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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send message");
            ErrorMessage = $"Failed to generate image: {ex.Message}";
            notificationService.Show("Error", ex.Message, NotificationType.Error);
            CanRetryLastMessage = true; // Enable retry on error
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task RetryLastMessageAsync()
    {
        if (lastMessageText == null && lastMessageImagePaths == null)
            return;

        // Restore the last message
        NewMessageText = lastMessageText ?? string.Empty;

        // Restore pending images
        if (lastMessageImagePaths != null)
        {
            PendingImages.Clear();
            foreach (var imagePath in lastMessageImagePaths)
            {
                if (File.Exists(imagePath))
                {
                    try
                    {
                        var bitmap = new Bitmap(imagePath);
                        PendingImages.Add(new PendingImage { FilePath = imagePath, Bitmap = bitmap });
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to reload image for retry: {ImagePath}", imagePath);
                    }
                }
            }
        }

        // Send the message again
        await SendMessageAsync(CancellationToken.None);
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
    }

    [RelayCommand]
    private void ClearPendingImages()
    {
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
                        PendingImages[index] = new PendingImage
                        {
                            FilePath = annotatedPath,
                            Bitmap = annotatedBitmap,
                        };

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
        // When provider changes, check if we can update the current conversation
        if (CurrentConversation != null && value != null && value != CurrentConversation.ProviderId)
        {
            // If no messages have been sent, allow changing the provider
            if (Messages.Count == 0)
            {
                _ = UpdateConversationProviderAsync(value);
            }
            else
            {
                notificationService.Show(
                    "Provider Changed",
                    "Create a new conversation to use the selected provider.",
                    NotificationType.Information
                );
            }
        }

        // If switching away from local providers, clean up any pending connection
        if (value is not ("flux-kontext" or "qwen-image-edit"))
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
        if (value == "flux-kontext")
        {
            LoadAvailableFluxModels();

            // Auto-show missing models dialog if connected and models are missing
            _ = CheckAndShowMissingModelsDialogAsync();
        }

        // Load available Qwen models when switching to Qwen Image Edit
        if (value == "qwen-image-edit")
        {
            LoadAvailableQwenModels();

            // Auto-show missing models dialog if connected and models are missing
            _ = CheckAndShowMissingModelsDialogAsync();
        }
    }

    /// <summary>
    /// Check for missing models and auto-show the download dialog if needed
    /// </summary>
    private async Task CheckAndShowMissingModelsDialogAsync()
    {
        // Don't show if we've already shown it this session
        if (hasShownMissingModelsDialog)
            return;

        // Wait a moment for connection status to settle
        await Task.Delay(500);

        // Only show if connected and models are missing
        if (!ClientManager.IsConnected || !HasMissingModels)
            return;

        hasShownMissingModelsDialog = true;
        await ShowMissingModelsDialogAsync();
    }

    /// <summary>
    /// Updates the current conversation's provider (only if no messages sent yet)
    /// </summary>
    private async Task UpdateConversationProviderAsync(string newProviderId)
    {
        if (CurrentConversation == null)
            return;

        try
        {
            // Create updated conversation with new provider
            var updatedConversation = CurrentConversation with
            {
                ProviderId = newProviderId,
            };
            await chatService.UpdateConversationAsync(updatedConversation);

            // Update local reference
            var index = Conversations.IndexOf(CurrentConversation);
            if (index >= 0)
            {
                Conversations[index] = updatedConversation;
            }
            CurrentConversation = updatedConversation;

            logger.LogInformation(
                "Updated conversation {Id} provider to {Provider}",
                updatedConversation.Id,
                newProviderId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update conversation provider");
            notificationService.Show(
                "Error",
                "Failed to update conversation provider",
                NotificationType.Error
            );
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
    /// Whether there are missing models that can be downloaded
    /// </summary>
    [ObservableProperty]
    public partial bool HasMissingModels { get; set; }

    /// <summary>
    /// Show the missing models download dialog
    /// </summary>
    [RelayCommand]
    private async Task ShowMissingModelsDialogAsync()
    {
        if (!ClientManager.IsConnected)
        {
            notificationService.Show(
                "Not Connected",
                "Please connect to ComfyUI first to check for missing models.",
                NotificationType.Warning
            );
            return;
        }

        // Get the model manager for the current provider
        var modelManager = LocalProviderModelManagerRegistry.GetManager(SelectedProviderId);
        if (modelManager == null)
        {
            logger.LogWarning("No model manager found for provider {ProviderId}", SelectedProviderId);
            return;
        }

        var missingModels = modelManager.GetMissingModels(ClientManager).ToList();

        if (missingModels.Count == 0)
        {
            notificationService.Show(
                "All Models Present",
                "All required models are already installed!",
                NotificationType.Success
            );
            return;
        }

        logger.LogInformation(
            "Showing missing models dialog for {Provider} with {Count} models",
            modelManager.ProviderDisplayName,
            missingModels.Count
        );

        // Create and configure the dialog using manager's properties
        var dialogVm = vmFactory.Get<DownloadMissingModelsViewModel>();
        dialogVm.DialogTitle = $"{modelManager.ProviderDisplayName} Setup";
        dialogVm.Description = modelManager.DownloadDialogDescription;
        dialogVm.SetModels(missingModels);

        var dialog = dialogVm.GetDialog();
        var result = await dialog.ShowAsync();

        // If user clicked Download, start the downloads
        if (result == ContentDialogResult.Primary && dialogVm.SelectedCount > 0)
        {
            // Start downloads (runs in background via TrackedDownloadService)
            var downloads = await dialogVm.StartDownloadsAsync();

            if (downloads.Count > 0)
            {
                notificationService.Show(
                    "Downloads Started",
                    $"Downloading {downloads.Count} model(s). Check the progress panel for status.",
                    NotificationType.Information
                );

                // Track completion of all downloads
                _ = TrackDownloadCompletionAsync(downloads, modelManager.ProviderDisplayName);
            }
        }
    }

    /// <summary>
    /// Track when all downloads complete and show notification
    /// </summary>
    private async Task TrackDownloadCompletionAsync(
        List<TrackedDownload> downloads,
        string providerDisplayName
    )
    {
        var completionTasks = downloads
            .Select(d =>
            {
                var tcs = new TaskCompletionSource<bool>();

                d.ProgressStateChanged += (s, state) =>
                {
                    if (state is ProgressState.Success or ProgressState.Failed or ProgressState.Cancelled)
                    {
                        tcs.TrySetResult(state == ProgressState.Success);
                    }
                };

                // Check if already completed
                if (
                    d.ProgressState
                    is ProgressState.Success
                        or ProgressState.Failed
                        or ProgressState.Cancelled
                )
                {
                    tcs.TrySetResult(d.ProgressState == ProgressState.Success);
                }

                return tcs.Task;
            })
            .ToList();

        // Wait for all downloads to complete
        var results = await Task.WhenAll(completionTasks);
        var successCount = results.Count(r => r);
        var failCount = results.Count(r => !r);

        logger.LogInformation(
            "Model downloads completed: {Success} succeeded, {Failed} failed",
            successCount,
            failCount
        );

        // Refresh model index
        await modelIndexService.RefreshIndex();

        // Reconnect to ComfyUI to refresh model lists
        if (ClientManager.IsConnected)
        {
            try
            {
                await ClientManager.ConnectAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to reconnect after model download");
            }
        }

        // Update status on UI thread
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            UpdateProviderStatus();
            LoadAvailableFluxModels();
            LoadAvailableQwenModels();
        });

        // Show completion notification
        if (failCount == 0 && successCount > 0)
        {
            notificationService.Show(
                "Models Ready! 🎉",
                $"All required models have been downloaded. {providerDisplayName} is ready to use!",
                NotificationType.Success,
                TimeSpan.FromSeconds(8)
            );
        }
        else if (successCount > 0)
        {
            notificationService.Show(
                "Downloads Partially Complete",
                $"{successCount} model(s) downloaded, {failCount} failed. Check the progress panel for details.",
                NotificationType.Warning
            );
        }
        else
        {
            notificationService.Show(
                "Downloads Failed",
                "All model downloads failed. Please check your connection and try again.",
                NotificationType.Error
            );
        }
    }

    /// <summary>
    /// Loads available Flux Kontext models from the DiffusionModels folder using local model index
    /// </summary>
    private void LoadAvailableFluxModels()
    {
        AvailableFluxModels.Clear();
        AvailableFluxLoras.Clear();

        // Load UNet models from local index - prioritize those with "Flux.1 Kontext" base model, then show untagged
        var kontextModels = new List<HybridModelFile>();
        var untaggedModels = new List<HybridModelFile>();

        var localUnetModels = modelIndexService
            .FindByModelType(SharedFolderType.DiffusionModels)
            .Select(HybridModelFile.FromLocal);

        foreach (var model in localUnetModels)
        {
            var baseModel = model.Local?.ConnectedModelInfo?.BaseModel;

            if (
                baseModel?.Contains("Kontext", StringComparison.OrdinalIgnoreCase) == true
                || baseModel?.Contains("Flux.1 Kontext", StringComparison.OrdinalIgnoreCase) == true
            )
            {
                kontextModels.Add(model);
            }
            else if (string.IsNullOrEmpty(baseModel))
            {
                // Also check filename for "kontext" as fallback
                if (model.FileName.Contains("kontext", StringComparison.OrdinalIgnoreCase))
                {
                    kontextModels.Add(model);
                }
                else
                {
                    untaggedModels.Add(model);
                }
            }
        }

        // Sort: connected models first, then alphabetically by display name
        var sortedKontextModels = kontextModels
            .OrderByDescending(m => m.Local?.ConnectedModelInfo != null)
            .ThenBy(m => m.Local?.DisplayModelName ?? m.ShortDisplayName);

        var sortedUntaggedModels = untaggedModels
            .OrderByDescending(m => m.Local?.ConnectedModelInfo != null)
            .ThenBy(m => m.Local?.DisplayModelName ?? m.ShortDisplayName);

        // Add Kontext models first, then untagged
        foreach (var model in sortedKontextModels)
        {
            AvailableFluxModels.Add(model);
        }
        foreach (var model in sortedUntaggedModels)
        {
            AvailableFluxModels.Add(model);
        }

        // Auto-select first Kontext model if available
        if (SelectedFluxModel == null && AvailableFluxModels.Count > 0)
        {
            SelectedFluxModel =
                AvailableFluxModels.FirstOrDefault(m =>
                    m.FileName.Contains("kontext", StringComparison.OrdinalIgnoreCase)
                ) ?? AvailableFluxModels.First();
        }

        // Load LoRA models from local index - prioritize Flux Kontext, then Flux, then untagged
        var kontextLoras = new List<HybridModelFile>();
        var fluxLoras = new List<HybridModelFile>();
        var untaggedLoras = new List<HybridModelFile>();

        var localLoraModels = modelIndexService
            .FindByModelType(SharedFolderType.Lora | SharedFolderType.LyCORIS)
            .Select(HybridModelFile.FromLocal);

        foreach (var lora in localLoraModels)
        {
            var baseModel = lora.Local?.ConnectedModelInfo?.BaseModel;

            if (baseModel?.Contains("Kontext", StringComparison.OrdinalIgnoreCase) == true)
            {
                kontextLoras.Add(lora);
            }
            else if (baseModel?.Contains("Flux", StringComparison.OrdinalIgnoreCase) == true)
            {
                fluxLoras.Add(lora);
            }
            else if (string.IsNullOrEmpty(baseModel))
            {
                untaggedLoras.Add(lora);
            }
        }

        // Sort LoRAs: connected models first, then alphabetically
        var sortedKontextLoras = kontextLoras
            .OrderByDescending(l => l.Local?.ConnectedModelInfo != null)
            .ThenBy(l => l.Local?.DisplayModelName ?? l.ShortDisplayName);
        var sortedFluxLoras = fluxLoras
            .OrderByDescending(l => l.Local?.ConnectedModelInfo != null)
            .ThenBy(l => l.Local?.DisplayModelName ?? l.ShortDisplayName);
        var sortedUntaggedLoras = untaggedLoras
            .OrderByDescending(l => l.Local?.ConnectedModelInfo != null)
            .ThenBy(l => l.Local?.DisplayModelName ?? l.ShortDisplayName);

        foreach (var lora in sortedKontextLoras)
            AvailableFluxLoras.Add(lora);
        foreach (var lora in sortedFluxLoras)
            AvailableFluxLoras.Add(lora);
        foreach (var lora in sortedUntaggedLoras)
            AvailableFluxLoras.Add(lora);

        logger.LogInformation(
            "Loaded {ModelCount} Flux models and {LoraCount} LoRAs from local index",
            AvailableFluxModels.Count,
            AvailableFluxLoras.Count
        );
    }

    /// <summary>
    /// Loads available Qwen Image Edit models from the DiffusionModels folder using local model index
    /// </summary>
    private void LoadAvailableQwenModels()
    {
        AvailableQwenModels.Clear();
        AvailableQwenLoras.Clear();

        // Load UNet models from local index - prioritize those with "qwen" in name or base model
        var qwenModels = new List<HybridModelFile>();
        var untaggedModels = new List<HybridModelFile>();

        var localUnetModels = modelIndexService
            .FindByModelType(SharedFolderType.DiffusionModels)
            .Select(HybridModelFile.FromLocal);

        foreach (var model in localUnetModels)
        {
            var baseModel = model.Local?.ConnectedModelInfo?.BaseModel;

            if (baseModel?.Contains("Qwen", StringComparison.OrdinalIgnoreCase) == true)
            {
                qwenModels.Add(model);
            }
            else if (string.IsNullOrEmpty(baseModel))
            {
                // Also check filename for "qwen" as fallback
                if (model.FileName.Contains("qwen", StringComparison.OrdinalIgnoreCase))
                {
                    qwenModels.Add(model);
                }
                else
                {
                    untaggedModels.Add(model);
                }
            }
        }

        // Sort: connected models first, then alphabetically by display name
        var sortedQwenModels = qwenModels
            .OrderByDescending(m => m.Local?.ConnectedModelInfo != null)
            .ThenBy(m => m.Local?.DisplayModelName ?? m.ShortDisplayName);

        var sortedUntaggedModels = untaggedModels
            .OrderByDescending(m => m.Local?.ConnectedModelInfo != null)
            .ThenBy(m => m.Local?.DisplayModelName ?? m.ShortDisplayName);

        // Add Qwen models first, then untagged
        foreach (var model in sortedQwenModels)
        {
            AvailableQwenModels.Add(model);
        }
        foreach (var model in sortedUntaggedModels)
        {
            AvailableQwenModels.Add(model);
        }

        // Auto-select first Qwen model if available
        if (SelectedQwenModel == null && AvailableQwenModels.Count > 0)
        {
            SelectedQwenModel =
                AvailableQwenModels.FirstOrDefault(m =>
                    m.FileName.Contains("qwen", StringComparison.OrdinalIgnoreCase)
                ) ?? AvailableQwenModels.First();
        }

        // Load LoRA models from local index - all LoRAs are potentially compatible
        var untaggedLoras = new List<HybridModelFile>();

        var localLoraModels = modelIndexService
            .FindByModelType(SharedFolderType.Lora | SharedFolderType.LyCORIS)
            .Select(HybridModelFile.FromLocal);

        foreach (var lora in localLoraModels)
        {
            untaggedLoras.Add(lora);
        }

        // Sort LoRAs: connected models first, then alphabetically
        var sortedLoras = untaggedLoras
            .OrderByDescending(l => l.Local?.ConnectedModelInfo != null)
            .ThenBy(l => l.Local?.DisplayModelName ?? l.ShortDisplayName);

        foreach (var lora in sortedLoras)
            AvailableQwenLoras.Add(lora);

        logger.LogInformation(
            "Loaded {ModelCount} Qwen models and {LoraCount} LoRAs from local index",
            AvailableQwenModels.Count,
            AvailableQwenLoras.Count
        );
    }

    [RelayCommand]
    private async Task AddLoraAsync()
    {
        // Get available LoRAs based on current provider
        var availableLoras =
            SelectedProviderId == "qwen-image-edit" ? AvailableQwenLoras : AvailableFluxLoras;

        if (availableLoras.Count == 0)
        {
            notificationService.Show(
                "No LoRAs Available",
                "No compatible LoRA models found.",
                NotificationType.Warning
            );
            return;
        }

        // Create a styled selection dialog using BetterComboBox with HybridModel theme
        var comboBox = new BetterComboBox
        {
            ItemsSource = availableLoras,
            SelectedIndex = 0,
            MinWidth = 350,
            Padding = new Thickness(8, 6, 4, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        // Apply the HybridModel theme
        if (
            App.Current?.Resources.TryGetResource(
                "BetterComboBoxHybridModelTheme",
                App.Current.ActualThemeVariant,
                out var theme
            ) == true
            && theme is ControlTheme controlTheme
        )
        {
            comboBox.Theme = controlTheme;
        }

        var dialog = new ContentDialog
        {
            Title = "Add LoRA",
            Content = comboBox,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && comboBox.SelectedItem is HybridModelFile selectedLora)
        {
            // Check if already added
            if (SelectedLoras.Any(l => l.Model.RelativePath == selectedLora.RelativePath))
            {
                notificationService.Show(
                    "Already Added",
                    "This LoRA is already in the list.",
                    NotificationType.Warning
                );
                return;
            }

            SelectedLoras.Add(new SelectedLora { Model = selectedLora });
        }
    }

    [RelayCommand]
    private void RemoveLora(SelectedLora lora)
    {
        SelectedLoras.Remove(lora);
    }

    [RelayCommand]
    private void ToggleFluxSettings()
    {
        IsFluxSettingsExpanded = !IsFluxSettingsExpanded;
    }

    [RelayCommand]
    private void ToggleQwenSettings()
    {
        IsQwenSettingsExpanded = !IsQwenSettingsExpanded;
    }
}

/// <summary>
/// Represents an image pending to be sent
/// </summary>
public class PendingImage
{
    public required string FilePath { get; init; }
    public required Bitmap Bitmap { get; init; }
}

/// <summary>
/// Represents a provider for display in the ComboBox
/// </summary>
public record ProviderDisplayItem(string Id, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// Represents a selected LoRA with weight settings
/// </summary>
public partial class SelectedLora : ObservableObject
{
    public required HybridModelFile Model { get; init; }

    [ObservableProperty]
    private decimal modelWeight = 1.0m;

    [ObservableProperty]
    private decimal clipWeight = 1.0m;

    public string DisplayName => Model.Local?.DisplayModelName ?? Model.ShortDisplayName;
}

/// <summary>
/// Represents an aspect ratio option for image generation
/// </summary>
public record AspectRatioOption(string Ratio, string Description, int Width, int Height)
{
    public string DisplayName => $"{Ratio} - {Description} ({Width}x{Height})";

    public override string ToString() => DisplayName;
}
