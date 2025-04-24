using System;
using DiscordRPC;
using DiscordRPC.Logging;
using DiscordRPC.Message;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

public class DiscordRichPresenceService : IDiscordRichPresenceService
{
    private const string ApplicationId = "1134669805237059615";

    private readonly ILogger<DiscordRichPresenceService> logger;
    private readonly ISettingsManager settingsManager;
    private readonly DiscordRpcClient client;
    private readonly string appDetails;
    private bool isDisposed;

    private RichPresence DefaultPresence =>
        new()
        {
            Details = appDetails,
            Assets = new DiscordRPC.Assets
            {
                LargeImageKey = "stabilitymatrix-logo-1",
                LargeImageText = $"Stability Matrix {appDetails}",
            },
            Buttons = new[]
            {
                new Button { Label = "GitHub", Url = "https://github.com/LykosAI/StabilityMatrix", }
            }
        };

    public DiscordRichPresenceService(
        ILogger<DiscordRichPresenceService> logger,
        ISettingsManager settingsManager
    )
    {
        this.logger = logger;
        this.settingsManager = settingsManager;

        appDetails = $"v{Compat.AppVersion.WithoutMetadata()}";

        client = new DiscordRpcClient(ApplicationId);
        client.Logger = new NullLogger();
        client.OnReady += OnReady;
        client.OnError += OnError;
        client.OnClose += OnClose;
        client.OnPresenceUpdate += OnPresenceUpdate;

        settingsManager.SettingsPropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(settingsManager.Settings.IsDiscordRichPresenceEnabled))
            {
                UpdateState();
            }
        };

        EventManager.Instance.RunningPackageStatusChanged += OnRunningPackageStatusChanged;
    }

    private void OnReady(object sender, ReadyMessage args)
    {
        logger.LogInformation("Received Ready from user {User}", args.User.Username);
    }

    private void OnError(object sender, ErrorMessage args)
    {
        logger.LogWarning("Received Error: {Message}", args.Message);
    }

    private void OnClose(object sender, CloseMessage args)
    {
        logger.LogInformation("Received Close: {Reason}", args.Reason);
    }

    private void OnPresenceUpdate(object sender, PresenceMessage args)
    {
        logger.LogDebug("Received Update: {Presence}", args.Presence.ToString());
    }

    private void OnRunningPackageStatusChanged(object? sender, RunningPackageStatusChangedEventArgs args)
    {
        if (!client.IsInitialized || !settingsManager.Settings.IsDiscordRichPresenceEnabled)
            return;

        if (args.CurrentPackagePair is null)
        {
            client.SetPresence(DefaultPresence);
        }
        else
        {
            var presence = DefaultPresence;

            var packageTitle = args.CurrentPackagePair.BasePackage switch
            {
                FluxGym => "FluxGym",
                Fooocus => "Fooocus",
                Reforge => "SD WebUI reForge",
                Sdfx => "SDFX",
                StableSwarm => "SwarmUI",
                SDWebForge or ForgeAmdGpu => "SD WebUI Forge",
                KohyaSs => "KohyaSS",
                OneTrainer => "OneTrainer",
                Cogstudio => "Cogstudio",
                ComfyUI or ComfyZluda => "ComfyUI",
                InvokeAI => "InvokeAI",
                VoltaML => "VoltaML",
                VladAutomatic => "SD.Next Web UI",
                A3WebUI => "Automatic1111 Web UI",
                _ => "Stable Diffusion"
            };

            presence.State = $"Running {packageTitle}";

            presence.Assets.SmallImageText = presence.State;
            presence.Assets.SmallImageKey = args.CurrentPackagePair.BasePackage switch
            {
                ComfyUI => "fa_diagram_project",
                VoltaML => "ic_package_voltaml",
                InvokeAI => "ic_package_invokeai",
                _ => "ic_fluent_box_512_filled"
            };

            presence.WithTimestamps(
                new Timestamps
                {
                    StartUnixMilliseconds = (ulong?)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                }
            );

            client.SetPresence(presence);
        }
    }

    public void UpdateState()
    {
        // Set initial rich presence
        if (settingsManager.Settings.IsDiscordRichPresenceEnabled)
        {
            lock (client)
            {
                if (!client.IsInitialized)
                {
                    client.Initialize();
                    client.SetPresence(DefaultPresence);
                }
            }
        }
        else
        {
            lock (client)
            {
                if (client.IsInitialized)
                {
                    client.ClearPresence();
                    client.Deinitialize();
                }
            }
        }
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            if (client.IsInitialized)
            {
                client.ClearPresence();
            }
            client.Dispose();
            EventManager.Instance.RunningPackageStatusChanged -= OnRunningPackageStatusChanged;
        }

        isDisposed = true;
        GC.SuppressFinalize(this);
    }

    ~DiscordRichPresenceService()
    {
        Dispose();
    }
}
