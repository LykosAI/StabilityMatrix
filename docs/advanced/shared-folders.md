# Shared Folders

Stability Matrix keeps a shared model library so compatible packages can use the same files. Model sharing and output sharing are separate per-package settings; enabling one does not enable the other.

[`Section Overview`](overview.md) | [`Home`](../README.md)

## Table of Contents

- [The Shared Library](#the-shared-library)
- [Model Sharing Methods](#model-sharing-methods)
- [ComfyUI Path Examples](#comfyui-path-examples)
- [Output Sharing](#output-sharing)
- [Change Sharing or Storage Locations](#change-sharing-or-storage-locations)
- [Troubleshooting Shared Models](#troubleshooting-shared-models)

---

## The Shared Library

The shared model root is normally `<data directory>/Models`. Each type has a category below it, such as `StableDiffusion`, `Lora`, or `VAE`. A models folder selected separately in Settings replaces that root.

Each package maps the categories it supports to its own folder names or configuration keys. Sharing a file does not make it compatible with another architecture or loader. For example, a package can see a model on disk but still lack the functionality needed to load it.

For common category destinations and local imports, see [Checkpoint Manager](../checkpoint-manager/overview.md#where-models-live).

## Model Sharing Methods

Open **Packages → the installation's three-dot menu → Model Sharing**. Only supported methods are available for a given package.

| Method | How the package accesses models | What happens to package-local model files |
|---|---|---|
| **Symlink** | Package model folders point to the shared library. Stability Matrix uses directory junctions on Windows and symbolic links on other platforms. | When setting up a mapped folder, the app attempts to move existing files into the shared category before replacing the folder with a link. |
| **Config** | The app writes shared paths into the package's supported configuration file. Called **Configuration** in installation options. | Local model folders can remain alongside shared paths; those local files are not relocated by configuration sharing. |
| **None** | Stability Matrix does not configure shared model paths for that installation. | The package uses its own folders or paths you configure yourself. |

ComfyUI recommends **Configuration** in this checkout and uses `extra_model_paths.yaml`. An empty `models/checkpoints` folder inside a Config-mode ComfyUI installation is therefore not evidence that sharing failed.

With Symlink sharing, deleting a model *through* a linked folder deletes the same file used by other packages. A linked folder is another path to the file, not a backup copy.

## ComfyUI Path Examples

These mappings show why a shared category and a ComfyUI folder can have different names. The Config column lists keys managed in ComfyUI's extra model paths file; it does not imply that a physical link exists.

| Stability Matrix category | ComfyUI folder in Symlink mode | Config key |
|---|---|---|
| `StableDiffusion` | `models/checkpoints` | `checkpoints` |
| `Lora`, `LyCORIS` | `models/loras` | `loras` |
| `VAE` | `models/vae` | `vae` |
| `TextEncoders` | `models/clip` | `clip` |
| `DiffusionModels` | `models/diffusion_models` | `diffusion_models` |
| `Embeddings` | `models/embeddings` | `embeddings` |

Keep files in the Stability Matrix categories when following instructions that describe ComfyUI's own folders. For example, a text encoder intended for ComfyUI's `models/clip` belongs in the shared `TextEncoders` category when using this mapping.

## Output Sharing

For supported packages, **Use Shared Output Folder** connects mapped output folders to categories under `<data directory>/Images`. The mappings vary by package and need not cover every kind of file a package writes.

ComfyUI's `output` folder maps to `Images/Text2Img`. Outputs saved by Stability Matrix's built-in Inference pipeline use `Images/Inference`. These are distinct output paths even when ComfyUI is the backend for both interfaces.

With output sharing disabled, outputs can remain inside the package installation. Check the actual destination before uninstalling it. Package-local files are removed with the package; shared files outside it remain. See [Uninstall a Package](../package-manager/managing-packages.md#uninstall-a-package).

## Change Sharing or Storage Locations

Stop the affected package before changing its sharing method. If its local model folders already contain files, preserve anything you need before switching: Symlink setup can relocate those files, whereas Config sharing leaves them local. Review any operation error and verify the resulting paths before deleting old copies.

Switching to **None** does not copy the shared library into the package. Models previously reached through managed paths may no longer appear there until you configure local files or restore sharing.

To store models on another drive, copy or move the existing model categories into the new models root, then use **Settings → System → Select New Models Folder** to select that root. This setting creates the shared category folders and requires an app restart; it does not transfer your existing models. Do not delete the old copy until the Checkpoint Manager and a launched package both see the intended files. For moving the entire library, follow [Data Directory](../getting-started/data-directory.md#changing-the-data-directory-later).

**Settings → Checkpoint Manager → Remove Symlinks on Shutdown** removes managed model links when Stability Matrix closes. If enabled, packages launched outside Stability Matrix after shutdown may not see models through those links. Start them through Stability Matrix to let it set up sharing again.

## Troubleshooting Shared Models

| Symptom | Check |
|---|---|
| Model appears in the manager but not the package | Correct installation, sharing method, supported category, and successful package restart. |
| ComfyUI's local model folders are empty | Whether **Config** sharing is in use; inspect `extra_model_paths.yaml` in the package folder. |
| Models disappear after moving a drive or library | Whether configured paths and link targets still resolve to the actual files. |
| A link cannot be created | That the destination is writable, the drive supports links, and existing custom links do not create a loop. See [Data Directory](../getting-started/data-directory.md#a-note-on-disk-space). |
| A model is listed but cannot load | Model architecture, loader support, and required companion files; sharing only provides file access. |

Capture package console errors or the application log when sharing setup fails. See [Finding Logs and Reporting Bugs](../troubleshooting/common-issues.md#finding-logs-and-reporting-bugs).
