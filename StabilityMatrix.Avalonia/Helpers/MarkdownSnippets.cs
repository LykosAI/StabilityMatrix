namespace StabilityMatrix.Avalonia.Helpers;

public static class MarkdownSnippets
{
    public static string SMFolderMap =>
        """
        # Stability Matrix Folder Reference
        Some guides and tutorials will mention folders by the names used in other WebUIs like ComfyUI, A1111, or others.
        Use this table to find the correct folder when following external guides:
        
        | If a guide says to put it in... | In Stability Matrix, use this folder |
        |---------------------------------|--------------------------------------|
        | Checkpoints                     | StableDiffusion                      |
        | CLIP                            | TextEncoders                         |
        | unet                            | DiffusionModels                      |
        | upscale_models                  | ESRGAN                               |
        
        You can find this table again by clicking the 3-dots (...) -> Folder Reference button in the top right corner of the Checkpoint Manager.
        """;

    public static string SharedFolderMigration =>
        """
        # Shared Folder Migration
        Some folder names have been updated to better reflect the contents of the folder and cause less confusion.
        Your models have been safely migrated to the new folders.

        | Old Name           | New Name        |
        |--------------------|-----------------|
        | Unet               | DiffusionModels |
        | InvokeClipVision   | ClipVision      |
        | InvokeIpAdapters15 | IpAdapters15    |
        | InvokeIpAdaptersXl | IpAdaptersXl    |
        | TextualInversion   | Embeddings      |
        """;
}
