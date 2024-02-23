using System.Text.Json;
using System.Text.Json.Nodes;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models.Api.OpenArt;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.PackageModification;

public class DownloadOpenArtWorkflowStep(
    IOpenArtApi openArtApi,
    OpenArtSearchResult workflow,
    ISettingsManager settingsManager
) : IPackageStep
{
    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        var workflowData = await openArtApi
            .DownloadWorkflowAsync(new OpenArtDownloadRequest { WorkflowId = workflow.Id })
            .ConfigureAwait(false);

        Directory.CreateDirectory(settingsManager.WorkflowDirectory);
        var filePath = Path.Combine(settingsManager.WorkflowDirectory, $"{workflowData.Filename}.json");

        var jsonObject = JsonNode.Parse(workflowData.Payload) as JsonObject;
        jsonObject?.Add("workflow_id", workflow.Id);
        jsonObject?.Add("workflow_name", workflow.Name);
        jsonObject?.Add("creator", workflow.Creator.Username);
        var thumbs = new JsonArray();
        foreach (var thumb in workflow.Thumbnails)
        {
            thumbs.Add(thumb.Url);
        }
        jsonObject?.Add("thumbnails", thumbs);

        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(jsonObject)).ConfigureAwait(false);

        progress?.Report(new ProgressReport(1f, "Downloaded OpenArt Workflow"));
    }

    public string ProgressTitle => "Downloading OpenArt Workflow";
}
