using System;
using System.IO;
using System.Windows.Threading;
using AutoUpdaterDotNET;
using Microsoft.Extensions.Logging;

namespace StabilityMatrix.Helper;

public class UpdateHelper : IUpdateHelper
{
    private readonly ILogger<UpdateHelper> logger;
    private readonly DispatcherTimer timer = new();

    public UpdateHelper(ILogger<UpdateHelper> logger)
    {
        this.logger = logger;
        timer.Interval = TimeSpan.FromMinutes(5);
        timer.Tick += (_, _) =>
        {
            CheckForUpdate();
        };
        AutoUpdater.CheckForUpdateEvent += AutoUpdaterOnCheckForUpdateEvent;
    }

    public void StartCheckingForUpdates()
    {
        timer.IsEnabled = true;
        timer.Start();
        CheckForUpdate();
    }

    private void CheckForUpdate()
    {
        AutoUpdater.DownloadPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Update");
        AutoUpdater.ExecutablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Update", "StabilityMatrix.exe");
        // TODO: make this github url?
        AutoUpdater.Start("https://cdn.lykos.ai/update.xml");
    }

    private void AutoUpdaterOnCheckForUpdateEvent(UpdateInfoEventArgs args)
    {
        if (args.Error == null && args.IsUpdateAvailable)
        {
            EventManager.Instance.OnUpdateAvailable(args);
        }
        else if (args.Error != null)
        {
            logger.LogError(args.Error, "Error while checking for update");
        }
    }
}
