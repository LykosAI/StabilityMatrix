using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.PackageModification;

namespace StabilityMatrix.Avalonia.Helpers;

public static class ConsoleProcessRunner
{
    public static async Task<ContentDialogValueResult<string?>> GetArgumentDialogResultAsync(
        string title,
        string fieldLabel,
        [Localizable(false)] string fieldInnerLeftText
    )
    {
        var fields = new TextBoxField[]
        {
            new() { Label = fieldLabel, InnerLeftText = fieldInnerLeftText }
        };

        var dialog = DialogHelper.CreateTextEntryDialog(title, "", fields);
        var result = await dialog.ShowAsync();

        return new ContentDialogValueResult<string?>(result, fields[0].Text);
    }

    public static async Task<PackageModificationRunner> RunProcessStepAsync(ProcessStep step)
    {
        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            CloseWhenFinished = false,
            ModificationCompleteMessage =
                $"Process command executed successfully for '{Path.GetFileName(step.FileName)}'",
            ModificationFailedMessage = $"Process command failed for '{Path.GetFileName(step.FileName)}'"
        };

        EventManager.Instance.OnPackageInstallProgressAdded(runner);

        await runner.ExecuteSteps([step]).ConfigureAwait(false);

        return runner;
    }
}
