# Data Directory

The data directory (also called the library) is the single folder where Stability Matrix keeps everything it manages: installed packages, shared model storage, generated images, downloaded tools, and its own settings file. This page explains what lives in that folder, where it goes by default, and how to choose or relocate it.

[`Section Overview`](overview.md) | [`Home`](../README.md)

## Table of Contents

- [What the Data Directory Is](#what-the-data-directory-is)
- [What Lives Inside It](#what-lives-inside-it)
- [Default Location](#default-location)
- [Portable Mode](#portable-mode)
- [Changing the Data Directory Later](#changing-the-data-directory-later)
- [A Note on Disk Space](#a-note-on-disk-space)
- [What's Next](#whats-next)

---

## What the Data Directory Is

Stability Matrix stores all of the data it manages under one root folder, referred to internally as the library. The location is chosen during [first launch](first-launch.md) and can be changed later. Whenever the app needs to install a package, share a model, or save an output, it works relative to this one directory.

The data directory always contains a `settings.json` file at its root. When the **Select Data Directory** dialog validates a folder, it treats the folder as an existing Stability Matrix library if a readable `settings.json` is present, and otherwise accepts the folder only if it is empty.

## What Lives Inside It

Stability Matrix creates and manages several subfolders inside the data directory. The main ones are:

- **`Packages/`** — Each installed package is cloned into its own subfolder here, named after its display name (for example `Packages/ComfyUI`). This includes the package's own files and its Python virtual environment.
- **`Models/`** — The shared model library. Rather than every package keeping its own copy of large files, models are stored once here and shared. This folder is organized into type-based subfolders such as `StableDiffusion` (checkpoints), `Lora`, `VAE`, `ControlNet`, `ESRGAN` (upscalers), `Embeddings`, and others. The location of the `Models` folder can be pointed elsewhere with a model directory override in settings.
- **`Images/`** — The shared outputs folder. When output sharing is enabled for a package, its generated images are saved here instead of inside the package folder. Inference UI outputs are kept under `Images/Inference`.
- **`Assets/`** — Portable tooling that Stability Matrix downloads and manages for you, such as the `uv` utility, bundled Python installations, 7-Zip, and (for packages that need it) Node.js. Keeping these here means no system-wide Python or Git install is required.
- **`Workflows/`** — Saved ComfyUI workflows managed through the app.
- **`Tags/`** — Tag autocomplete data used by the Inference UI.
- **`.downloads/`** — A working folder for in-progress downloads.

The root also holds the `settings.json` configuration file. This layout is why the data directory can grow large, and why it is treated as a single portable unit.

## Default Location

The default location depends on your operating system and on whether Portable Mode is enabled.

For a non-portable install, the default library path is:

| Platform | Default location |
|---|---|
| Windows | `%AppData%\StabilityMatrix` (the Roaming AppData folder) |
| Linux | `StabilityMatrix` inside your home directory (`~/StabilityMatrix`) |
| macOS | The application data directory, which resolves to `~/.config/StabilityMatrix` |

For non-portable installs, Stability Matrix records the chosen library path in a `library.json` file kept in its AppData home folder, and reads that on startup to find your data directory.

## Portable Mode

Portable Mode keeps the data directory next to the application instead of sending it to one of the default locations above. When Portable Mode is used, the library is a folder named `Data` alongside the Stability Matrix executable, marked by a `.sm-portable` file inside it.

Portable Mode is enabled by default in the **Select Data Directory** dialog, and it is the recommended option for most users. Because the app and its `Data` folder stay bundled together, the whole setup is easier to move to another folder, drive, or computer later. On startup, Stability Matrix checks for the portable marker first, so a portable `Data` folder always takes precedence over any saved non-portable path.

## Changing the Data Directory Later

The data directory can be changed after setup from the app's settings, which reopens the same **Select Data Directory** dialog used during first launch. You can either pick a new custom folder or switch to Portable Mode. Applying the change requires restarting Stability Matrix.

Changing the setting only updates where Stability Matrix *looks* for its library — it points the app at the new location (and creates that folder if needed) but does not move your existing packages, models, or images for you. If you want to keep your current data, move or copy the contents of the old data directory to the new location yourself before or after switching, then confirm the app finds a valid `settings.json` there.

## A Note on Disk Space

Choose the data directory location with disk space in mind. The bulk of the space usage comes from the `Packages` and `Models` folders. A single package install is commonly in the multi-gigabyte range once its PyTorch dependencies are downloaded, and individual model checkpoints are frequently several gigabytes to tens of gigabytes each. Over time a model library can easily reach hundreds of gigabytes.

Prefer a drive with plenty of free space, and ideally a fast one. FAT32 and exFAT drives are not supported, so pick a drive formatted with a modern filesystem (such as NTFS on Windows); the **Select Data Directory** dialog shows a warning if the chosen drive uses a FAT format. Placing the library inside a synced cloud folder such as OneDrive is also discouraged, and the dialog shows a warning when it detects a OneDrive path.

## What's Next

- [First Launch](first-launch.md) — Where the data directory is first chosen
- [Installing Packages](../package-manager/installing-packages.md) — What gets written into `Packages/`
- Shared Folders *(planned)* — How the `Models/` library is shared across packages
