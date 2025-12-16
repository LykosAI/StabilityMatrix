using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Net.Http;
using System.Net.Sockets;
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
using FluentAvalonia.UI.Media.Animation;
using Injectio.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.ViewModels.Settings;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
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
    private readonly IServiceManager<ViewModelBase> vmFactory;
    private readonly IModelIndexService modelIndexService;
    private readonly INavigationService<MainWindowViewModel> navigationService;
    private readonly INavigationService<SettingsViewModel> settingsNavigationService;

    public override string Title => "Image Lab";
    public override IconSource IconSource => new FASymbolIconSource { Symbol = "fa-solid fa-flask" };

    public IInferenceClientManager ClientManager { get; }

    [ObservableProperty]
    public partial string? NewMessageText { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    [NotifyPropertyChangedFor(nameof(IsCurrentConversationGenerating))]
    public partial bool IsGenerating { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCurrentConversationGenerating))]
    public partial Guid? GeneratingConversationId { get; set; }

    /// <summary>
    /// True if the currently selected conversation is the one that's generating.
    /// Used to scope the progress indicator to the active conversation.
    /// </summary>
    public bool IsCurrentConversationGenerating =>
        IsGenerating && CurrentConversation != null && GeneratingConversationId == CurrentConversation.Id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGenerationProgress))]
    [NotifyPropertyChangedFor(nameof(GenerationProgressText))]
    public partial int? GenerationProgressPercent { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGenerationProgress))]
    [NotifyPropertyChangedFor(nameof(GenerationProgressText))]
    public partial string? GenerationProgressStage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGenerationProgress))]
    [NotifyPropertyChangedFor(nameof(GenerationProgressText))]
    public partial string? GenerationProgressRunningNode { get; set; }

    public bool HasGenerationProgress =>
        RequiresLocalBackend
        && (
            GenerationProgressPercent != null
            || !string.IsNullOrEmpty(GenerationProgressStage)
            || !string.IsNullOrEmpty(GenerationProgressRunningNode)
        );

    public string GenerationProgressText
    {
        get
        {
            if (!IsGenerating)
                return "Ready";

            if (!RequiresLocalBackend)
                return "Creating your image...";

            var stage = string.IsNullOrWhiteSpace(GenerationProgressStage)
                ? "Creating your image..."
                : GenerationProgressStage;
            var node = string.IsNullOrWhiteSpace(GenerationProgressRunningNode)
                ? null
                : GenerationProgressRunningNode.Replace('_', ' ');
            var percent = GenerationProgressPercent;

            if (percent is >= 0 and <= 100 && !string.IsNullOrWhiteSpace(node))
                return $"{stage} ({percent}%) • {node}";
            if (percent is >= 0 and <= 100)
                return $"{stage} ({percent}%)";
            if (!string.IsNullOrWhiteSpace(node))
                return $"{stage} • {node}";

            return stage;
        }
    }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCurrentConversationGenerating))]
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
            loadMessagesCts = new();
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
            ClearMessages();
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
        await Dispatcher.UIThread.InvokeAsync(ClearMessages);

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
                    AddMessageToUI(message);
                }

                // Notify gallery that images may have changed
                OnPropertyChanged(nameof(ConversationImages));
                OnPropertyChanged(nameof(HasConversationImages));

                // If this conversation is currently generating, re-add the loading placeholder
                if (GeneratingConversationId == conversation.Id && IsGenerating)
                {
                    currentLoadingMessage = new LoadingImageMessage
                    {
                        TargetWidth = (SelectedAspectRatio?.Width ?? 300) / 3,
                        TargetHeight = (SelectedAspectRatio?.Height ?? 300) / 3,
                    };
                    Messages.Add(currentLoadingMessage);
                }

                // Start the view at the bottom when switching to a (potentially long) conversation.
                // Guard against late completion after the user already switched away.
                if (CurrentConversation?.Id == conversation.Id)
                {
                    Dispatcher.UIThread.Post(
                        () => ScrollToEndForcedRequested?.Invoke(this, EventArgs.Empty),
                        DispatcherPriority.Background
                    );
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
    /// Whether we can regenerate the last assistant response (true when there's at least one assistant message)
    /// </summary>
    public bool CanRegenerateLastResponse =>
        Messages.OfType<TextMessage>().Any(m => !m.IsMyMessage)
        || Messages.OfType<ImageMessage>().Any(m => !m.IsMyMessage);

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
            new("1:1", "Square", 1024, 1024),
            new("16:9", "Landscape Wide", 1344, 768),
            new("9:16", "Portrait Tall", 768, 1344),
            new("4:3", "Landscape", 1152, 896),
            new("3:4", "Portrait", 896, 1152),
            new("3:2", "Photo Landscape", 1216, 832),
            new("2:3", "Photo Portrait", 832, 1216),
            new("21:9", "Ultrawide", 1536, 640),
            new("9:21", "Ultra Tall", 640, 1536),
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

    /// <summary>
    /// Whether the image gallery sidebar is visible
    /// </summary>
    [ObservableProperty]
    public partial bool IsGalleryVisible { get; set; }

    /// <summary>
    /// Gets all images in the current conversation for the gallery view
    /// </summary>
    public IEnumerable<ImageMessage> ConversationImages =>
        Messages.OfType<ImageMessage>().Where(m => !m.IsMyMessage);

    /// <summary>
    /// Whether there are any images in the conversation
    /// </summary>
    public bool HasConversationImages => ConversationImages.Any();

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
    /// Tracks the current loading message so it can be reliably removed on cancellation.
    /// </summary>
    private LoadingImageMessage? currentLoadingMessage;

    /// <summary>
    /// Messages in the current conversation. Can contain MessageBase or ThinkingMessage.
    /// </summary>
    public ObservableCollection<object> Messages { get; }

    /// <summary>
    /// Event raised when the message list should scroll to the end
    /// </summary>
    public event EventHandler? ScrollToEndRequested;

    /// <summary>
    /// Event raised when the message list should force-scroll to the end.
    /// Used after switching conversations so users start at the bottom immediately.
    /// </summary>
    public event EventHandler? ScrollToEndForcedRequested;

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
        IModelIndexService modelIndexService,
        INavigationService<MainWindowViewModel> navigationService,
        INavigationService<SettingsViewModel> settingsNavigationService
    )
    {
        this.logger = logger;
        this.chatService = chatService;
        this.secretsManager = secretsManager;
        this.notificationService = notificationService;
        this.vmFactory = vmFactory;
        this.navigationService = navigationService;
        this.settingsNavigationService = settingsNavigationService;
        this.modelIndexService = modelIndexService;

        ClientManager = inferenceClientManager;

        // Initialize Messages collection and subscribe to changes for auto-scroll
        Messages = [];
        Messages.CollectionChanged += OnMessagesCollectionChanged;

        // Load available providers
        var providers = chatService.GetAvailableProviders();
        foreach (var provider in providers)
        {
            AvailableProviders.Add(new(provider.ProviderId, provider.ProviderName));
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

            // When disconnected during generation, cancel the pending operation
            if (!ClientManager.IsConnected && IsGenerating && RequiresLocalBackend)
            {
                logger.LogWarning("ComfyUI disconnected during generation, cancelling...");
                CancelGeneration();
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

    private void ResetGenerationProgress()
    {
        GenerationProgressPercent = null;
        GenerationProgressStage = null;
        GenerationProgressRunningNode = null;
        OnPropertyChanged(nameof(GenerationProgressText));
    }

    private IProgress<ImageGenerationProgress> CreateProgressReporter(string providerId)
    {
        return new Progress<ImageGenerationProgress>(progress =>
        {
            // Only show progress for the active local generation session/provider.
            if (!RequiresLocalBackend || SelectedProviderId != providerId)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                GenerationProgressPercent = progress.Percent;
                GenerationProgressStage = progress.Stage;
                GenerationProgressRunningNode = progress.RunningNode;
                OnPropertyChanged(nameof(GenerationProgressText));
            });
        });
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
                ClearMessages();
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
        const int maxRetries = 5;
        const int retryDelayMs = 1000;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                logger.LogInformation(
                    "Attempting to connect to ComfyUI (attempt {Attempt}/{MaxRetries})...",
                    attempt,
                    maxRetries
                );
                await ClientManager.ConnectAsync();
                notificationService.Show(
                    "Connected",
                    "Successfully connected to ComfyUI",
                    NotificationType.Success
                );
                return; // Success - exit the method
            }
            catch (HttpRequestException ex)
                when (ex.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused })
            {
                // Connection refused - ComfyUI might still be starting up
                if (attempt < maxRetries)
                {
                    logger.LogDebug(
                        "Connection refused (attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms...",
                        attempt,
                        maxRetries,
                        retryDelayMs
                    );
                    await Task.Delay(retryDelayMs);
                }
                else
                {
                    logger.LogWarning(
                        ex,
                        "Failed to connect to ComfyUI after {MaxRetries} attempts",
                        maxRetries
                    );
                    notificationService.Show(
                        "Connection Failed",
                        "Could not connect to ComfyUI. Make sure it's running and try again.",
                        NotificationType.Warning
                    );
                }
            }
            catch (Exception ex)
            {
                // Other errors - don't retry
                logger.LogError(ex, "Failed to connect to ComfyUI");
                notificationService.Show("Connection Failed", ex.Message, NotificationType.Error);
                return;
            }
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

        var messageText = NewMessageText;
        var imagePaths = PendingImages.Select(p => p.FilePath).ToList();

        // Store for retry
        lastMessageText = messageText;
        lastMessageImagePaths = imagePaths.Count > 0 ? imagePaths : null;

        NewMessageText = string.Empty;
        ErrorMessage = null;
        CanRetryLastMessage = false;

        // Add user message to UI immediately
        var provisionalUiItems = new List<object>();
        if (!string.IsNullOrWhiteSpace(messageText))
        {
            var uiText = new TextMessage(messageText, true);
            provisionalUiItems.Add(uiText);
            Messages.Add(uiText);
        }

        // Show pending images in chat (provisional; will be replaced by persisted copies after DB save)
        foreach (var pendingImage in PendingImages)
        {
            var uiImage = new ImageMessage(pendingImage.Bitmap, true);
            provisionalUiItems.Add(uiImage);
            Messages.Add(uiImage);
        }

        // Clear pending images
        PendingImages.Clear();

        IsGenerating = true;
        ResetGenerationProgress();
        if (RequiresLocalBackend && !string.IsNullOrEmpty(SelectedProviderId))
        {
            GenerationProgressStage = "Starting...";
        }

        // Track which conversation is generating (for restoring placeholder on switch back)
        GeneratingConversationId = CurrentConversation.Id;

        // Add loading placeholder (scaled to 1/3 of target size for compact display)
        currentLoadingMessage = new LoadingImageMessage
        {
            TargetWidth = (SelectedAspectRatio?.Width ?? 300) / 3,
            TargetHeight = (SelectedAspectRatio?.Height ?? 300) / 3,
        };
        Messages.Add(currentLoadingMessage);

        try
        {
            // Build provider options
            var providerOptions = BuildProviderOptions();
            var progress =
                RequiresLocalBackend && SelectedProviderId != null
                    ? CreateProgressReporter(SelectedProviderId)
                    : null;

            var (userMessage, assistantMessage) = await chatService.SendMessageAsync(
                CurrentConversation.Id,
                SelectedProviderId!,
                messageText,
                imagePaths.Count > 0 ? imagePaths : null,
                providerOptions,
                progress,
                cancellationToken
            );

            // Remove loading placeholder
            if (currentLoadingMessage != null)
            {
                Messages.Remove(currentLoadingMessage);
                currentLoadingMessage = null;
            }

            // Replace provisional user UI items with canonical DB-backed messages (with IDs and persisted image paths).
            foreach (var item in provisionalUiItems)
            {
                Messages.Remove(item);
                if (item is ImageMessage imageMessage)
                {
                    imageMessage.Image?.Dispose();
                }
            }

            AddUserMessageToUI(userMessage);

            // Add assistant response to UI
            if (assistantMessage != null)
            {
                AddAssistantMessageToUI(assistantMessage);
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
            // Check if cancellation was due to connection loss
            if (RequiresLocalBackend && !ClientManager.IsConnected)
            {
                logger.LogWarning("Message generation cancelled due to connection loss");
                ErrorMessage = "Connection to ComfyUI was lost during generation.";
                notificationService.Show(
                    "Connection Lost",
                    "ComfyUI disconnected during generation",
                    NotificationType.Warning
                );
            }
            else
            {
                logger.LogInformation("Message generation cancelled");
                ErrorMessage = "Cancelled";
            }
            CanRetryLastMessage = true;
        }
        catch (ImageGenerationException ex)
        {
            // Expected error from generation (provider error, API error, etc.)
            logger.LogWarning("Image generation failed: {Message}", ex.Message);

            // Check if this is an API key error
            if (ex.Message.Contains("API key", StringComparison.OrdinalIgnoreCase))
            {
                await ShowApiKeyRequiredDialogAsync();
                CanRetryLastMessage = true;
            }
            else
            {
                ErrorMessage = ex.Message;
                notificationService.Show("Generation Failed", ex.Message, NotificationType.Warning);
                CanRetryLastMessage = true;
            }
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
            GeneratingConversationId = null;
            ResetGenerationProgress();
            // Ensure loading placeholder is removed on cancel/error
            if (currentLoadingMessage != null)
            {
                Messages.Remove(currentLoadingMessage);
                currentLoadingMessage = null;
            }
        }
    }

    /// <summary>
    /// Shows a dialog prompting the user to add their Gemini API key in settings
    /// </summary>
    private async Task ShowApiKeyRequiredDialogAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "API Key Required",
            Content =
                "Gemini API key not configured. Please add your Gemini API key in Account Settings to use cloud providers.",
            PrimaryButtonText = "Open Settings",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            // Navigate to Settings -> Account Settings
            navigationService.NavigateTo<SettingsViewModel>(new SuppressNavigationTransitionInfo());
            await Task.Delay(100);
            settingsNavigationService.NavigateTo<AccountSettingsViewModel>(
                new SuppressNavigationTransitionInfo()
            );
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RetryLastMessageAsync(CancellationToken cancellationToken)
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
        ResetGenerationProgress();
        if (RequiresLocalBackend && !string.IsNullOrEmpty(SelectedProviderId))
        {
            GenerationProgressStage = "Starting...";
        }

        // Track which conversation is generating (for restoring placeholder on switch back)
        GeneratingConversationId = CurrentConversation.Id;

        try
        {
            // Build provider options
            var providerOptions = BuildProviderOptions();
            var progress =
                RequiresLocalBackend && SelectedProviderId != null
                    ? CreateProgressReporter(SelectedProviderId)
                    : null;

            // Add loading placeholder (scaled to 1/3 of target size for compact display)
            currentLoadingMessage = new LoadingImageMessage
            {
                TargetWidth = (SelectedAspectRatio?.Width ?? 300) / 3,
                TargetHeight = (SelectedAspectRatio?.Height ?? 300) / 3,
            };
            Messages.Add(currentLoadingMessage);

            // Retry generation - this doesn't create a new user message
            var assistantMessage = await chatService.RetryGenerationAsync(
                CurrentConversation.Id,
                SelectedProviderId,
                providerOptions,
                progress,
                cancellationToken
            );

            // Remove loading placeholder
            if (currentLoadingMessage != null)
            {
                Messages.Remove(currentLoadingMessage);
                currentLoadingMessage = null;
            }

            // Add only the assistant response to UI
            AddAssistantMessageToUI(assistantMessage, includeDbId: false);

            // Reload conversations to update timestamps
            await LoadConversationsAsync();
        }
        catch (OperationCanceledException)
        {
            // Check if cancellation was due to connection loss
            if (RequiresLocalBackend && !ClientManager.IsConnected)
            {
                logger.LogWarning("Retry generation cancelled due to connection loss");
                ErrorMessage = "Connection to ComfyUI was lost during generation.";
                notificationService.Show(
                    "Connection Lost",
                    "ComfyUI disconnected during generation",
                    NotificationType.Warning
                );
            }
            else
            {
                logger.LogInformation("Retry generation cancelled");
                ErrorMessage = "Cancelled";
            }
            CanRetryLastMessage = true;
        }
        catch (ImageGenerationException ex)
        {
            logger.LogWarning("Retry generation failed: {Message}", ex.Message);

            // Check if this is an API key error
            if (ex.Message.Contains("API key", StringComparison.OrdinalIgnoreCase))
            {
                await ShowApiKeyRequiredDialogAsync();
                CanRetryLastMessage = true;
            }
            else
            {
                ErrorMessage = ex.Message;
                notificationService.Show("Generation Failed", ex.Message, NotificationType.Warning);
                CanRetryLastMessage = true;
            }
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
            GeneratingConversationId = null;
            ResetGenerationProgress();
            // Ensure loading placeholder is removed on cancel/error
            if (currentLoadingMessage != null)
            {
                Messages.Remove(currentLoadingMessage);
                currentLoadingMessage = null;
            }
        }
    }

    [RelayCommand]
    private void DismissError()
    {
        ErrorMessage = null;
        CanRetryLastMessage = false;
    }

    /// <summary>
    /// Regenerates the last assistant response (without an error context)
    /// </summary>
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RegenerateLastResponseAsync(CancellationToken cancellationToken)
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
        ResetGenerationProgress();
        if (RequiresLocalBackend && !string.IsNullOrEmpty(SelectedProviderId))
        {
            GenerationProgressStage = "Starting...";
        }

        // Track which conversation is generating (for restoring placeholder on switch back)
        GeneratingConversationId = CurrentConversation.Id;

        try
        {
            // Remove the last assistant message(s) from UI before regenerating
            var messagesToRemove = new List<object>();
            for (var i = Messages.Count - 1; i >= 0; i--)
            {
                var message = Messages[i];
                // Stop when we hit a user message
                if (message is TextMessage tm && tm.IsMyMessage)
                    break;
                if (message is ImageMessage im && im.IsMyMessage)
                    break;

                messagesToRemove.Add(message);
            }

            // Remove in reverse order to avoid index issues
            foreach (var message in messagesToRemove)
            {
                Messages.Remove(message);
                // Dispose image if needed
                if (message is ImageMessage imageMessage)
                {
                    imageMessage.Image?.Dispose();
                }
            }

            // Delete old assistant messages from database (but preserve their image files)
            var dbMessages = await chatService.GetMessagesAsync(CurrentConversation.Id);
            var lastUserMessage = dbMessages.LastOrDefault(m => m.Role == MessageRole.User);
            if (lastUserMessage != null)
            {
                // Find all assistant messages after the last user message
                var oldAssistantMessages = dbMessages
                    .Where(m => m.Role == MessageRole.Assistant && m.Timestamp > lastUserMessage.Timestamp)
                    .ToList();

                // Delete them from database but preserve image files for the output browser
                foreach (var oldMsg in oldAssistantMessages)
                {
                    await chatService.DeleteMessageAsync(oldMsg.Id, preserveImageFile: true);
                }

                if (oldAssistantMessages.Count > 0)
                {
                    logger.LogInformation(
                        "Removed {Count} old assistant message(s) from database, preserved image files",
                        oldAssistantMessages.Count
                    );
                }
            }

            // Build provider options
            var providerOptions = BuildProviderOptions();
            var progress =
                RequiresLocalBackend && SelectedProviderId != null
                    ? CreateProgressReporter(SelectedProviderId)
                    : null;

            // Add loading placeholder (scaled to 1/3 of target size for compact display)
            currentLoadingMessage = new LoadingImageMessage
            {
                TargetWidth = (SelectedAspectRatio?.Width ?? 300) / 3,
                TargetHeight = (SelectedAspectRatio?.Height ?? 300) / 3,
            };
            Messages.Add(currentLoadingMessage);

            // Retry generation - this doesn't create a new user message
            var assistantMessage = await chatService.RetryGenerationAsync(
                CurrentConversation.Id,
                SelectedProviderId,
                providerOptions,
                progress,
                cancellationToken
            );

            // Remove loading placeholder
            if (currentLoadingMessage != null)
            {
                Messages.Remove(currentLoadingMessage);
                currentLoadingMessage = null;
            }

            // Add the new assistant response to UI
            var addedAnyImages = AddAssistantMessageToUI(assistantMessage, includeDbId: false);

            if (addedAnyImages)
            {
                // Notify gallery
                OnPropertyChanged(nameof(ConversationImages));
                OnPropertyChanged(nameof(HasConversationImages));
            }

            // Reload conversations to update timestamps
            await LoadConversationsAsync();

            // Notify property change
            OnPropertyChanged(nameof(CanRegenerateLastResponse));
        }
        catch (OperationCanceledException)
        {
            // Check if cancellation was due to connection loss
            if (RequiresLocalBackend && !ClientManager.IsConnected)
            {
                logger.LogWarning("Regenerate cancelled due to connection loss");
                ErrorMessage = "Connection to ComfyUI was lost during generation.";
                notificationService.Show(
                    "Connection Lost",
                    "ComfyUI disconnected during generation",
                    NotificationType.Warning
                );
            }
            else
            {
                logger.LogInformation("Regenerate cancelled");
                ErrorMessage = "Cancelled";
            }
            CanRetryLastMessage = true;
        }
        catch (ImageGenerationException ex)
        {
            logger.LogWarning("Regenerate failed: {Message}", ex.Message);

            // Check if this is an API key error
            if (ex.Message.Contains("API key", StringComparison.OrdinalIgnoreCase))
            {
                await ShowApiKeyRequiredDialogAsync();
                CanRetryLastMessage = true;
            }
            else
            {
                ErrorMessage = ex.Message;
                notificationService.Show("Generation Failed", ex.Message, NotificationType.Warning);
                CanRetryLastMessage = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during regenerate");
            ErrorMessage = $"Unexpected error: {ex.Message}";
            notificationService.Show("Error", ex.Message, NotificationType.Error);
            CanRetryLastMessage = true;
        }
        finally
        {
            IsGenerating = false;
            GeneratingConversationId = null;
            ResetGenerationProgress();
            // Ensure loading placeholder is removed on cancel/error
            if (currentLoadingMessage != null)
            {
                Messages.Remove(currentLoadingMessage);
                currentLoadingMessage = null;
            }
        }
    }

    /// <summary>
    /// Edits a user message with option to save only or save and regenerate
    /// </summary>
    [RelayCommand]
    private async Task EditUserMessageAsync(TextMessage? message)
    {
        if (message == null || !message.IsMyMessage || CurrentConversation == null)
            return;

        try
        {
            var existingMessageId = message.DatabaseMessageId;

            // Show edit dialog with two action options
            var textBox = new TextBox
            {
                Text = message.Text,
                Watermark = "Edit your message...",
                MinWidth = 400,
                MinHeight = 100,
                AcceptsReturn = true,
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            };

            var dialog = new ContentDialog
            {
                Title = "Edit Message",
                Content = textBox,
                PrimaryButtonText = "Save & Regenerate",
                SecondaryButtonText = "Save Only",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.None || string.IsNullOrWhiteSpace(textBox.Text))
                return;

            var editedText = textBox.Text.Trim();
            var shouldRegenerate = result == ContentDialogResult.Primary;

            // Get all messages from database
            var dbMessages = await chatService.GetMessagesAsync(CurrentConversation.Id);
            var dbMessage =
                existingMessageId != null
                    ? dbMessages.FirstOrDefault(m => m.Id == existingMessageId.Value)
                    : null;

            if (dbMessage == null)
            {
                // Message doesn't have a DatabaseMessageId - this is legacy data from before we tracked IDs.
                // We cannot safely edit these messages because mapping UI messages to database entries
                // is unreliable (a single database message can contain both text and images, but they
                // appear as separate UI elements). Refuse to edit to prevent data corruption.
                logger.LogWarning(
                    "Cannot edit message without DatabaseMessageId - legacy message from before ID tracking"
                );
                notificationService.Show(
                    "Cannot Edit",
                    "This message cannot be edited because it was created before message tracking was added. "
                        + "You can still send new messages normally.",
                    NotificationType.Warning
                );
                return;
            }

            if (shouldRegenerate)
            {
                // Original behavior: delete from this point and regenerate
                await EditAndRegenerateAsync(message, dbMessage, dbMessages, editedText);
            }
            else
            {
                // New behavior: just update the text without regenerating
                await EditMessageOnlyAsync(message, dbMessage, editedText);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to edit message");
            notificationService.Show(
                "Error",
                $"Failed to edit message: {ex.Message}",
                NotificationType.Error
            );
        }
    }

    /// <summary>
    /// Updates a message's text without regenerating subsequent messages
    /// </summary>
    private async Task EditMessageOnlyAsync(
        TextMessage uiMessage,
        ImageGenerationMessage dbMessage,
        string newText
    )
    {
        // Update in database
        var updatedMessage = await chatService.UpdateMessageTextAsync(dbMessage.Id, newText);
        if (updatedMessage == null)
        {
            notificationService.Show("Error", "Failed to update message", NotificationType.Error);
            return;
        }

        // Replace the UI message (TextMessage.Text is read-only)
        var index = Messages.IndexOf(uiMessage);
        if (index >= 0)
        {
            Messages[index] = new TextMessage(newText, uiMessage.IsMyMessage)
            {
                DatabaseMessageId = dbMessage.Id,
            };
        }

        logger.LogInformation("Updated message {MessageId} text without regeneration", dbMessage.Id);
        notificationService.Show("Message Updated", "Your message has been saved.", NotificationType.Success);
    }

    /// <summary>
    /// Edits a message and regenerates the conversation from that point
    /// </summary>
    private async Task EditAndRegenerateAsync(
        TextMessage uiMessage,
        ImageGenerationMessage dbMessage,
        List<ImageGenerationMessage> allDbMessages,
        string editedText
    )
    {
        // Delete all UI messages from this point onward
        var firstUiIndexToDelete = -1;
        for (var i = 0; i < Messages.Count; i++)
        {
            if (GetDatabaseMessageId(Messages[i]) == dbMessage.Id)
            {
                firstUiIndexToDelete = i;
                break;
            }
        }

        if (firstUiIndexToDelete < 0)
        {
            firstUiIndexToDelete = Messages.IndexOf(uiMessage);
        }

        var messagesToRemove = Messages.Skip(firstUiIndexToDelete).ToList();
        foreach (var msg in messagesToRemove)
        {
            Messages.Remove(msg);
            if (msg is ImageMessage im)
            {
                im.Image?.Dispose();
            }
        }

        // Delete all database messages from this point onward
        var messagesToDelete = allDbMessages
            .Where(m => m.Timestamp >= dbMessage.Timestamp)
            .OrderBy(m => m.Timestamp)
            .ToList();

        foreach (var msg in messagesToDelete)
        {
            await chatService.DeleteMessageAsync(msg.Id);
        }

        // Now send the edited message
        IsGenerating = true;
        ErrorMessage = null;

        try
        {
            // Add edited user message to UI
            Messages.Add(new TextMessage(editedText, true));

            // Build provider options
            var providerOptions = BuildProviderOptions();

            // Send the edited message
            var (userMessage, assistantMessage) = await chatService.SendMessageAsync(
                CurrentConversation!.Id,
                SelectedProviderId!,
                editedText,
                null,
                providerOptions,
                progress: null,
                CancellationToken.None
            );

            // Add assistant response to UI
            if (assistantMessage != null)
            {
                AddAssistantMessageToUI(assistantMessage);
            }

            // Reload conversations to update timestamps
            await LoadConversationsAsync();

            notificationService.Show(
                "Message Edited",
                "Your message has been edited and the conversation regenerated.",
                NotificationType.Success
            );
        }
        catch (ImageGenerationException ex)
        {
            logger.LogWarning("Failed to regenerate after edit: {Message}", ex.Message);

            if (ex.Message.Contains("API key", StringComparison.OrdinalIgnoreCase))
            {
                await ShowApiKeyRequiredDialogAsync();
                CanRetryLastMessage = true;
            }
            else
            {
                ErrorMessage = ex.Message;
                notificationService.Show("Generation Failed", ex.Message, NotificationType.Warning);
                CanRetryLastMessage = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error regenerating after edit");
            ErrorMessage = $"Unexpected error: {ex.Message}";
            notificationService.Show("Error", ex.Message, NotificationType.Error);
            CanRetryLastMessage = true;
        }
        finally
        {
            IsGenerating = false;
        }
    }

    /// <summary>
    /// Builds the provider options dictionary based on current settings
    /// </summary>
    private Dictionary<string, object> BuildProviderOptions()
    {
        Dictionary<string, object>? providerOptions = null;

        if (SupportsThinking && ShowThinkingOutput)
        {
            providerOptions = new() { ["enableThinking"] = true, ["thinkingBudget"] = 2048 };
        }

        if (SelectedProviderId == BananaVisionProviderIds.FluxKontext)
        {
            providerOptions ??= new();
            if (SelectedFluxModel != null)
                providerOptions["CustomUnetModel"] = SelectedFluxModel;
            if (SelectedLoras.Count > 0)
                providerOptions["SelectedLoras"] = SelectedLoras.ToList();
        }

        if (SelectedProviderId == BananaVisionProviderIds.QwenImageEdit)
        {
            providerOptions ??= new();
            if (SelectedQwenModel != null)
                providerOptions["CustomUnetModel"] = SelectedQwenModel;
            if (SelectedLoras.Count > 0)
                providerOptions["SelectedLoras"] = SelectedLoras.ToList();
        }

        providerOptions ??= new();

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
                new()
                {
                    Title = "Select Images",
                    AllowMultiple = true,
                    FileTypeFilter =
                    [
                        new("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif"] },
                    ],
                }
            );

            if (files.Count == 0)
                return;

            foreach (var file in files)
            {
                var imagePath = file.Path.LocalPath;
                var bitmap = new Bitmap(imagePath);

                PendingImages.Add(new() { FilePath = imagePath, Bitmap = bitmap });
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
    public void AddImagesFromPaths(IEnumerable<string> imagePaths)
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
                PendingImages.Add(new() { FilePath = imagePath, Bitmap = bitmap });
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
                        AddImagesFromPaths(imagePaths);
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
                if (data is byte[] { Length: > 0 } imageBytes)
                {
                    var tempPath = await SaveClipboardImageToTempFileAsync(imageBytes, format);
                    if (tempPath != null)
                    {
                        AddImagesFromPaths([tempPath]);
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
                            AddImagesFromPaths([tempPath]);
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
    private async Task CopyMessageAsync(TextMessage message)
    {
        try
        {
            if (string.IsNullOrEmpty(message.Text))
                return;

            var clipboard = App.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(message.Text);
                notificationService.Show("Copied", "Message copied to clipboard", NotificationType.Success);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to copy message to clipboard");
            notificationService.Show("Error", "Failed to copy message", NotificationType.Error);
        }
    }

    [RelayCommand]
    private async Task CopyImageToClipboardAsync(Bitmap? image)
    {
        if (image == null)
            return;

        try
        {
            if (Compat.IsWindows)
            {
                await WindowsClipboard.SetBitmapAsync(image);
                notificationService.Show("Copied", "Image copied to clipboard", NotificationType.Success);
            }
            else
            {
                notificationService.Show(
                    "Not Supported",
                    "Image clipboard is only supported on Windows",
                    NotificationType.Warning
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to copy image to clipboard");
            notificationService.Show("Error", "Failed to copy image", NotificationType.Error);
        }
    }

    /// <summary>
    /// Unified cancel command that stops any ongoing generation (Send, Retry, or Regenerate)
    /// </summary>
    [RelayCommand]
    private void CancelGeneration()
    {
        // Immediately remove loading placeholder for instant UI feedback
        if (currentLoadingMessage != null)
        {
            Messages.Remove(currentLoadingMessage);
            currentLoadingMessage = null;
        }

        // Cancel whichever operation is in progress
        if (SendMessageCancelCommand.CanExecute(null))
        {
            SendMessageCancelCommand.Execute(null);
        }
        if (RetryLastMessageCancelCommand.CanExecute(null))
        {
            RetryLastMessageCancelCommand.Execute(null);
        }
        if (RegenerateLastResponseCancelCommand.CanExecute(null))
        {
            RegenerateLastResponseCancelCommand.Execute(null);
        }
    }

    [RelayCommand]
    private void ToggleGallery()
    {
        IsGalleryVisible = !IsGalleryVisible;
        if (IsGalleryVisible)
        {
            OnPropertyChanged(nameof(ConversationImages));
        }
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
                        PendingImages[index] = new() { FilePath = annotatedPath, Bitmap = annotatedBitmap };
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
            viewerVm.ImageSource = new(bitmap);

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
    /// Gets all valid image paths from a message, handling both ImagePath and ImagePaths properties
    /// </summary>
    private static List<string> GetMessageImagePaths(ImageGenerationMessage message)
    {
        var paths =
            message.ImagePaths?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList()
            ?? (!string.IsNullOrEmpty(message.ImagePath) ? [message.ImagePath] : []);

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Adds a message (user or assistant) to the Messages collection
    /// </summary>
    /// <param name="message">The message to add</param>
    /// <param name="includeDbId">Whether to include the database message ID for tracking</param>
    /// <returns>True if any images were added</returns>
    private bool AddMessageToUI(ImageGenerationMessage message, bool includeDbId = true)
    {
        var isUserMessage = message.Role == MessageRole.User;
        var dbId = includeDbId ? message.Id : (Guid?)null;

        // Show thinking content first (only for assistant messages)
        if (!isUserMessage && ShowThinkingOutput && !string.IsNullOrEmpty(message.ThinkingContent))
        {
            Messages.Add(new ThinkingMessage(message.ThinkingContent) { DatabaseMessageId = dbId });
        }

        if (!string.IsNullOrEmpty(message.TextContent))
        {
            Messages.Add(new TextMessage(message.TextContent, isUserMessage) { DatabaseMessageId = dbId });
        }

        var addedAnyImages = false;
        foreach (var imagePath in GetMessageImagePaths(message).Where(File.Exists))
        {
            var bitmap = new Bitmap(imagePath);
            Messages.Add(
                new ImageMessage(bitmap, isUserMessage) { DatabaseMessageId = dbId, FilePath = imagePath }
            );
            addedAnyImages = true;
        }

        return addedAnyImages;
    }

    /// <summary>
    /// Adds an assistant message (thinking, text, and images) to the Messages collection
    /// </summary>
    private bool AddAssistantMessageToUI(ImageGenerationMessage message, bool includeDbId = true)
    {
        return AddMessageToUI(message, includeDbId);
    }

    /// <summary>
    /// Adds a user message (text and images) to the Messages collection
    /// </summary>
    private void AddUserMessageToUI(ImageGenerationMessage message)
    {
        AddMessageToUI(message, includeDbId: true);
    }

    /// <summary>
    /// Clears all messages and disposes any image bitmaps to prevent memory leaks
    /// </summary>
    private void ClearMessages()
    {
        foreach (var message in Messages)
        {
            if (message is ImageMessage imageMessage)
            {
                imageMessage.Image?.Dispose();
            }
        }
        Messages.Clear();
    }

    private static Guid? GetDatabaseMessageId(object? message)
    {
        return message switch
        {
            MessageBase m => m.DatabaseMessageId,
            ThinkingMessage tm => tm.DatabaseMessageId,
            _ => null,
        };
    }

    private void RemoveUiMessagesForDatabaseMessageId(Guid messageId)
    {
        var toRemove = Messages.Where(m => GetDatabaseMessageId(m) == messageId).ToList();

        foreach (var item in toRemove)
        {
            Messages.Remove(item);
            if (item is ImageMessage imageMessage)
            {
                imageMessage.Image?.Dispose();
            }
        }

        // Notify gallery that images may have changed
        OnPropertyChanged(nameof(ConversationImages));
        OnPropertyChanged(nameof(HasConversationImages));
        OnPropertyChanged(nameof(CanRegenerateLastResponse));
    }

    [RelayCommand]
    private async Task DeleteMessageAsync(object? messageItem)
    {
        if (CurrentConversation == null)
            return;

        var messageId = GetDatabaseMessageId(messageItem);
        if (messageId == null)
            return;

        try
        {
            // Check if this is an image from a multi-image message
            var isImageMessage =
                messageItem is ImageMessage imageMsg && !string.IsNullOrEmpty(imageMsg.FilePath);
            var dbMessage = isImageMessage ? await chatService.GetMessageAsync(messageId.Value) : null;
            var imageCount =
                dbMessage != null
                    ? (dbMessage.ImagePaths?.Count ?? (string.IsNullOrEmpty(dbMessage.ImagePath) ? 0 : 1))
                    : 0;
            var isMultiImageMessage = imageCount > 1;

            string dialogContent;
            if (isMultiImageMessage)
            {
                dialogContent =
                    "Delete this image from the message?\n\n"
                    + $"The message has {imageCount} images. Only this image will be removed.";
            }
            else
            {
                dialogContent =
                    "This will permanently delete the selected message from this conversation.\n\n"
                    + "Note: deleting a message in the middle of a conversation may change context for future generations.";
            }

            var dialog = new ContentDialog
            {
                Title = isMultiImageMessage ? "Delete image?" : "Delete message?",
                Content = new TextBlock
                {
                    Text = dialogContent,
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                    MaxWidth = 420,
                },
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            // Handle multi-image message: only remove the specific image
            if (
                isMultiImageMessage
                && messageItem is ImageMessage imgToDelete
                && !string.IsNullOrEmpty(imgToDelete.FilePath)
            )
            {
                var wasFullyDeleted = await chatService.RemoveImageFromMessageAsync(
                    messageId.Value,
                    imgToDelete.FilePath
                );

                if (wasFullyDeleted)
                {
                    // Whole message was deleted (was the last image)
                    RemoveUiMessagesForDatabaseMessageId(messageId.Value);
                }
                else
                {
                    // Only remove this specific UI element
                    Messages.Remove(messageItem);
                }
            }
            else
            {
                // Regular deletion - remove entire message
                await chatService.DeleteMessageAsync(messageId.Value);
                RemoveUiMessagesForDatabaseMessageId(messageId.Value);
            }

            // Reload conversations to update timestamps
            await LoadConversationsAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete message {MessageId}", messageId);
            notificationService.Show(
                "Error",
                $"Failed to delete message: {ex.Message}",
                NotificationType.Error
            );
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

        // Notify that regenerate availability may have changed
        OnPropertyChanged(nameof(CanRegenerateLastResponse));
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

            // Dispose message bitmaps and clear
            ClearMessages();

            // Unsubscribe from collection changed
            Messages.CollectionChanged -= OnMessagesCollectionChanged;
        }

        base.Dispose(disposing);
    }
}
