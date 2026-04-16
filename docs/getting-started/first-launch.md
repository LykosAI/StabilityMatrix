# First Launch

When a user starts Stability Matrix on a fresh install, the app walks through a short first-run setup. This flow is focused on accepting the license agreement, checking basic hardware compatibility, and choosing where application data will live before handing off to the main window.

[`Section Overview`](overview.md) | [`Home`](../README.md)

## Table of Contents

- [Welcome Window](#welcome-window)
- [License Agreement](#license-agreement)
- [Hardware Check and GPU Detection](#hardware-check-and-gpu-detection)
- [Selecting a Data Directory](#selecting-a-data-directory)
- [Migration Prompt for Existing Users](#migration-prompt-for-existing-users)
- [What You See Next](#what-you-see-next)

---

## Welcome Window

On first launch, Stability Matrix opens a small welcome window before the main application loads. This screen is only shown until the user accepts the license agreement. After that, later launches skip this step and open the main window directly.

This first window is intentionally simple. It is there to confirm the license agreement and show a quick compatibility check before the app continues.

If the user closes the window or chooses to quit instead of continuing, Stability Matrix exits without finishing setup.

## License Agreement

The welcome window includes a required checkbox confirming that the user has read and agrees to the Stability Matrix license agreement. The Continue button stays disabled until that checkbox is enabled.

There is also a direct link to open the full license text in the browser. Once the user continues, Stability Matrix records that acceptance and does not show the license step again on normal future launches.

## Hardware Check and GPU Detection

The same welcome window runs a quick hardware check in the background and shows the result as a status badge. It also displays the GPU the app detected, including its reported VRAM, when that information is available.

This check is mainly a compatibility warning, not a hard requirement. The badge reports success when an NVIDIA GPU is detected. If it does not find one, the user can still continue, but the app warns that some packages may not work as well and inference may be slower depending on the backend the user plans to use.

If no compatible GPU is detected at all, the app still allows the user to continue. Stability Matrix can still be useful for package management, downloads, and some CPU-backed or alternative-backend workflows, but package choices matter more in that situation.

## Selecting a Data Directory

After the welcome window is accepted, Stability Matrix opens the main window and then checks whether a library location has been configured. On a fresh setup, the user will normally be prompted to choose a data directory immediately.

The **Select Data Directory** dialog is where the user chooses the location that will hold packages, model checkpoints, LoRAs, settings, and related application data. This is one of the most important setup choices, because these files can grow large over time.

When choosing a location:

- Prefer a drive with enough free space for packages, models, and outputs.
- A dedicated SSD or fast secondary drive usually gives a better experience than a nearly full system drive.
- The Continue button is only enabled after the selected location passes validation.

The dialog also includes **Portable Mode**, and it is enabled by default. For most users, this is the recommended option because it keeps the application and its `Data` folder together, which makes the whole install much easier to move later if the user wants to relocate it to another folder, drive, or PC.

That portability is especially useful for larger setups, where packages, models, and related assets can take up a significant amount of space over time. Keeping everything bundled together reduces the chance of forgetting part of the install when moving it and makes backup or migration simpler.

For a deeper explanation of how the library path and portable mode work, see [Data Directory](data-directory.md).

## Migration Prompt for Existing Users

If Stability Matrix detects installed packages from an older package layout, the data-directory flow changes slightly. The selection dialog shows a welcome-back message, and after the data directory is chosen, the app can offer a migration step for those existing packages and related data.

This migration prompt is mainly intended for upgrades from older legacy layouts and/or default storage behavior. New users on a clean install, and users with pre-existing installs that already use the modern layout, usually will not see it.

## What You See Next

Once the license agreement is accepted and the data directory is configured, Stability Matrix finishes loading into the main window. If the user does not already have any installed packages, the app may then offer a one-click installer to help set up an initial web UI package, with ComfyUI being the recommended choice for use with the Inference UI. After that, Stability Matrix will also offer a selection of recommended models so the user can download a usable model right away. Both steps are optional and can be skipped if the user prefers to install packages or download models manually.

From there, the usual next steps are:

- [Install your first package](../package-manager/installing-packages.md)
- [Browse or import models](../model-browser/overview.md)
- If the user installed ComfyUI and downloaded a starter model during setup, they can [go straight to generating with the built-in Inference UI](../inference/overview.md)
