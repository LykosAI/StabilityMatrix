using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using NLog;
using Sentry;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(ExceptionDialog))]
[ManagedService]
[RegisterTransient<ExceptionViewModel>]
public partial class ExceptionViewModel : ViewModelBase
{
    public Exception? Exception { get; set; }

    public SentryId? SentryId { get; set; }

    public bool IsRecoverable { get; set; }

    public string Description =>
        IsRecoverable
            ? Resources.Text_UnexpectedErrorRecoverable_Description
            : Resources.Text_UnexpectedError_Description;

    public string? Message => Exception?.Message;

    public string? ExceptionType => Exception?.GetType().Name ?? "";

    public bool IsContinueResult { get; set; }

    public string? LogZipPath { get; set; }

    public static async Task<string> CreateLogFolderZip()
    {
        var tcs = new TaskCompletionSource();
        LogManager.Flush(
            ex =>
            {
                if (ex is null)
                {
                    tcs.SetResult();
                }
                else
                {
                    tcs.SetException(ex);
                }
            },
            TimeSpan.FromSeconds(15)
        );
        await tcs.Task;

        using var suspend = LogManager.SuspendLogging();

        var logDir = Compat.AppDataHome.JoinDir("Logs");

        // Copy logs to temp directory
        using var tempDir = new TempDirectoryPath();
        var tempLogDir = tempDir.JoinDir("Logs");
        tempLogDir.Create();
        foreach (var logFile in logDir.EnumerateFiles("*.log"))
        {
            // Need FileShare.ReadWrite since NLog keeps the file open
            await logFile.CopyToAsync(
                tempLogDir.JoinFile(logFile.Name),
                FileShare.ReadWrite,
                overwrite: true
            );
        }

        // Find a unique name for the output archive
        var archiveNameBase = $"stabilitymatrix-log-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}";
        var archiveName = archiveNameBase;
        var archivePath = Compat.AppDataHome.JoinFile(archiveName + ".zip");
        var i = 1;
        while (File.Exists(archivePath))
        {
            archiveName = $"{archiveNameBase}-{i++}";
            archivePath = Compat.AppDataHome.JoinFile(archiveName + ".zip");
        }

        // Create the archive
        ZipFile.CreateFromDirectory(tempLogDir, archivePath, CompressionLevel.Optimal, false);

        return archivePath;
    }

    [RelayCommand]
    private async Task OpenLogZipInFileBrowser()
    {
        if (string.IsNullOrWhiteSpace(LogZipPath) || !File.Exists(LogZipPath))
        {
            LogZipPath = await CreateLogFolderZip();
        }

        await ProcessRunner.OpenFileBrowser(LogZipPath);
    }

    [Localizable(false)]
    public string? FormatAsMarkdown()
    {
        var msgBuilder = new StringBuilder();
        msgBuilder.AppendLine();

        if (Exception is not null)
        {
            msgBuilder.AppendLine("## Exception");
            msgBuilder.AppendLine($"```{ExceptionType}: {Message}```");

            if (Exception.InnerException is not null)
            {
                msgBuilder.AppendLine(
                    $"```{Exception.InnerException.GetType().Name}: {Exception.InnerException.Message}```"
                );
            }
        }
        else
        {
            msgBuilder.AppendLine("## Exception");
            msgBuilder.AppendLine("```(None)```");
        }

        if (SentryId is { } id)
        {
            msgBuilder.AppendLine("### Sentry ID");
            msgBuilder.AppendLine($"[`{id.ToString()[..8]}`]({GetIssueUrl(id)})");
        }

        if (Exception?.StackTrace is not null)
        {
            msgBuilder.AppendLine("### Stack Trace");
            msgBuilder.AppendLine($"```{Exception.StackTrace}```");
        }

        if (Exception?.InnerException is { StackTrace: not null } innerException)
        {
            msgBuilder.AppendLine($"```{innerException.StackTrace}```");
        }

        return msgBuilder.ToString();
    }

    [Localizable(false)]
    private static string GetIssueUrl(SentryId sentryId)
    {
        return $"https://stability-matrix.sentry.io/issues/?query=id%3A{sentryId.ToString()}&referrer=sm-app-ex&statsPeriod=90d";
    }
}
