namespace StabilityMatrix.Core.Models.Settings;

public class ModelPickerFilterState
{
    public string SearchText { get; set; } = string.Empty;
    public bool ShowCheckpointsOnly { get; set; }
    public bool ShowUnetsOnly { get; set; }
    public List<string> SelectedBaseModels { get; set; } = [];
}
