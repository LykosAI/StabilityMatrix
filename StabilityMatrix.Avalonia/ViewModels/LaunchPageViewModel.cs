using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(LaunchPageView))]
public partial class LaunchPageViewModel : PageViewModelBase
{
    public override string Title => "Launch";
    public override Symbol Icon => Symbol.PlayFilled;

    [ObservableProperty]
    private TextDocument consoleDocument = new();

    public LaunchPageViewModel()
    {
        if (Design.IsDesignMode)
        {
            ConsoleDocument.Text = """
                                   Total VRAM 24576 MB, total RAM 64662 MB
                                   xformers version: 0.0.20
                                   Set vram state to: NORMAL_VRAM
                                   Device: cuda:0 NVIDIA GeForce RTX 3090
                                   Using xformers cross attention
                                   Starting server
                                   To see the GUI go to: http://127.0.0.1:8188
                                   """;
        }
    }

    [RelayCommand]
    private async Task LaunchAsync()
    {
        var info = new ProcessStartInfo
        {
            FileName = "py",
            WorkingDirectory = "py",
            Arguments = "-uc \"import tqdm, time; print('start'); [time.sleep(0.1) for _ in tqdm.tqdm(range(25))]; print('end')\""
        };

        var process = new AnsiProcess(info);
        process.Start();
        process.BeginAnsiRead(OnProcessOutputReceived);
        await process.WaitForExitAsync();
    }
    
    // Callback for processes
    private void OnProcessOutputReceived(ProcessOutput output)
    {
        var raw = output.RawText;
        // Replace /n and /r with literals
        raw = raw.Replace("\n", "\\n").Replace("\r", "\\r");
        Debug.WriteLine($"output raw: '{raw}', output: '{output.Text}', clear lines: {output.ClearLines}");
        Debug.Flush();
        Dispatcher.UIThread.Post(() =>
        {
            using var update = ConsoleDocument.RunUpdate();
            // Handle remove
            if (output.ClearLines > 0)
            {
                for (var i = 0; i < output.ClearLines; i++)
                {
                    var lastLineIndex = ConsoleDocument.LineCount - 1;
                    var line = ConsoleDocument.Lines[lastLineIndex];
                    ConsoleDocument.Remove(line.Offset, line.Length);
                }
            }
            // Add new line
            ConsoleDocument.Insert(ConsoleDocument.TextLength, output.Text);
        });
    }

    public override bool CanNavigateNext { get; protected set; }
    public override bool CanNavigatePrevious { get; protected set; }
}
