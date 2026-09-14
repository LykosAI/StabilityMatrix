# Checkpoint Manager

The **Checkpoint Manager** organizes your local model library: checkpoints, LoRAs, VAEs, text encoders, upscalers, and other model types. The **Model Browser** finds downloadable models; the Checkpoint Manager manages files already on disk.

[`Home`](../README.md)

## Table of Contents

- [Where Models Live](#where-models-live)
- [Import Local Models](#import-local-models)
- [Find and Organize Models](#find-and-organize-models)
- [Connected Metadata](#connected-metadata)
- [Rename, Move, and Delete Models](#rename-move-and-delete-models)
- [A Model Is Missing](#a-model-is-missing)
- [Related Pages](#related-pages)

---

## Where Models Live

By default, models live under `Models/` in your [data directory](../getting-started/data-directory.md). If you selected a separate models folder in Settings, that folder is the shared library instead. Use the **Models Folder** action to open the active location.

The folder tree groups files by type. Some common destinations are:

| File's role | Shared folder |
|---|---|
| Full checkpoint | `Models/StableDiffusion` |
| Standalone diffusion model | `Models/DiffusionModels` |
| LoRA | `Models/Lora` |
| VAE | `Models/VAE` |
| Text encoder | `Models/TextEncoders` |
| ControlNet | `Models/ControlNet` |
| ESRGAN upscaler | `Models/ESRGAN` |
| Textual inversion embedding | `Models/Embeddings` |

Use the file's documented role to choose its category. A `.safetensors` extension alone does not tell you whether the file is a checkpoint, LoRA, VAE, or another component. Moving a file to another category does not convert it to that model type.

The app's **Folder Reference** action provides additional folder information. [Shared Folders](../advanced/shared-folders.md) explains how packages read these files without keeping separate copies.

## Import Local Models

1. Check **Settings → Checkpoint Manager → Import Behaviour**. The default is **Move**, which relocates the original file. Select **Copy** if you want to keep the original where it is.
2. Open the Checkpoint Manager and select the destination category or a subfolder within it. Do not select the top-level models root; imports into that root are rejected.
3. Drag model files from your file manager onto the destination folder in the tree, or into the model area with that category selected.
4. Wait for **Import Complete**, then check the destination category. Use **Refresh** if needed.

Recognized import extensions are `.safetensors`, `.pt`, `.ckpt`, `.pth`, `.bin`, `.sft`, and `.gguf`. Recognition by the manager does not mean every package or Inference workflow can load that file.

Enable **Import with Metadata** to look for CivitAI information during import. This is optional; a local model can be imported without connected metadata. Downloaded archives and workflow JSON files are not model files for this import flow.

You can also place model files directly into the correct shared category using your file manager, then refresh the Checkpoint Manager. Select the model in the consuming package afterward; importing a file does not automatically select it for generation.

## Find and Organize Models

Select a category, then use **Search**, **Sort**, and **Filter** to narrow the cards shown. **Show Nested Models** includes models in subfolders of the selected category. Under **View**, you can adjust preview size and the visibility of empty root categories and NSFW content.

Right-click a folder to create a **New Folder**. For example, a subfolder under `StableDiffusion` can group checkpoints for a particular workflow. Keep files under their correct type category when organizing them.

Model cards can show a preview, filename, file size, and connected model information such as base model, version, and trigger words. Missing previews or source information do not by themselves mean that the model file is unusable.

## Connected Metadata

**Find Connected Metadata** looks for source information for local files. Use a model card's context menu for one file, or the toolbar scan for the selected category. **Update Existing Metadata** refreshes files that already have connected information.

A scan may report that no match exists, or that the service was unreachable or rate limited. Read the scan result before retrying. Repeated scans are not needed to use a model whose weights are already present.

The model context menu also exposes **Copy Trigger Words**, source navigation where available, **View Safetensor Metadata**, and **Edit Metadata**. Embedded safetensor information and connected source information are different: one comes from the model file, while the other describes a matched source model/version.

## Rename, Move, and Delete Models

Right-click a model for **Rename** or **Delete**. To move a model within the library, drag its card onto a destination folder. The manager moves its associated connected metadata and preview when present.

If **Move All Selected Models** is enabled, dragging a selected model can move the entire selection. Check the selection before dragging. Renaming or moving a model can also require reselecting it in saved projects or package workflows that refer to its old path.

**Delete** removes model files and their associated files after a confirmation dialog. This is a shared-library operation: every package using that model loses access to it. Review the listed paths, especially for a multi-selection or folder deletion.

## A Model Is Missing

1. Open **Models Folder** and confirm the file is in the active library, under the correct type category.
2. Clear search and filters, select the right category, and enable **Show Nested Models** if the file is in a subfolder.
3. Click **Refresh**. If the local index remains stale, see **Reset Checkpoints Cache** in [Settings](../settings/settings.md#checkpoint-manager).
4. If the Checkpoint Manager shows the file but a package does not, check that installation's **Model Sharing** setting and supported model types. Restart the package to reload its paths and model lists.
5. If the file is selectable but generation fails, check its required companion files and workflow compatibility. A standalone diffusion model may also need separate text encoders and a VAE.

## Related Pages

- [Shared Folders](../advanced/shared-folders.md)
- [Text to Image](../inference/text-to-image.md)
- [Settings: Checkpoint Manager](../settings/settings.md#checkpoint-manager)
- [Common Issues](../troubleshooting/common-issues.md)
