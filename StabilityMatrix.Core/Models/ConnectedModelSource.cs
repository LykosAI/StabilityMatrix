namespace StabilityMatrix.Core.Models;

public enum ConnectedModelSource
{
    Civitai,
    OpenModelDb,
    CivArchive,
    Other,

    // NOTE: Source is persisted to .cm-info.json as a numeric enum value, so new members must be
    // appended at the end. Inserting them mid-list shifts existing values and silently mislabels
    // already-saved models.
    HuggingFace,
}
