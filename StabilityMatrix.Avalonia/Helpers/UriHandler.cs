using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using StabilityMatrix.Core.Helper;
using URIScheme;

namespace StabilityMatrix.Avalonia.Helpers;

/// <summary>
/// Custom URI scheme handler for the stabilitymatrix:// protocol
/// </summary>
/// <remarks>Need to call <see cref="RegisterUriScheme"/> on App startup</remarks>
public class UriHandler
{
    public const string IpcKeySend = "uri_handler_send";

    public string Scheme { get; }
    public string Description { get; }

    public UriHandler(string scheme, string description)
    {
        Scheme = scheme;
        Description = description;
    }

    /// <summary>
    /// Send a received Uri over MessagePipe and exits
    /// </summary>
    /// <param name="uri"></param>
    [DoesNotReturn]
    public void SendAndExit(Uri uri)
    {
        var services = new ServiceCollection();
        services.AddMessagePipe().AddNamedPipeInterprocess("StabilityMatrix");

        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IDistributedPublisher<string, Uri>>();

        var sendTask = Task.Run(async () => await publisher.PublishAsync(IpcKeySend, uri));
        sendTask.Wait();

        var info = JsonSerializer.Serialize(new Dictionary<string, Uri> { [IpcKeySend] = uri });

        Debug.WriteLine(info);
        Console.WriteLine(info);

        Environment.Exit(0);
    }

    public void RegisterUriScheme()
    {
        if (Compat.IsWindows)
        {
            RegisterUriSchemeWin();
        }
        else if (Compat.IsLinux)
        {
            // Try to register on linux but ignore errors
            // Library does not support some distros
            try
            {
                RegisterUriSchemeLinux();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                Console.WriteLine(e);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private void RegisterUriSchemeWin()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@$"SOFTWARE\Classes\{Scheme}", true);

        key.SetValue("", "URL:" + Description);
        key.SetValue(null, Description);
        key.SetValue("URL Protocol", "");

        using (var defaultIcon = key.CreateSubKey("DefaultIcon"))
        {
            defaultIcon.SetValue("", Compat.AppCurrentPath.FullPath + ",1");
        }

        using (var commandKey = key.CreateSubKey(@"shell\open\command"))
        {
            commandKey.SetValue("", "\"" + Compat.AppCurrentPath.FullPath + "\" --uri \"%1\"");
        }
    }

    [SupportedOSPlatform("linux")]
    private void RegisterUriSchemeLinux()
    {
        var service = URISchemeServiceFactory.GetURISchemeSerivce(
            Scheme,
            Description,
            Compat.AppCurrentPath.FullPath
        );
        service.Set();
    }
}
