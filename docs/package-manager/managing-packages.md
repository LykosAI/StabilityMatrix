# Managing Packages

Use the **Packages** screen to launch installed tools, read their console output, change versions, and remove installations you no longer need. Each package card represents a separate installation with its own settings and Python environment.

[`Section Overview`](overview.md) | [`Home`](../README.md)

## Table of Contents

- [Launch, Console, and Web UI](#launch-console-and-web-ui)
- [Stop and Restart](#stop-and-restart)
- [Update a Package](#update-a-package)
- [Change Versions](#change-versions)
- [Per-Package Settings](#per-package-settings)
- [Uninstall a Package](#uninstall-a-package)
- [Related Pages](#related-pages)

---

## Launch, Console, and Web UI

1. Open **Packages** and find the installation you want to use.
2. Click **Launch**. Packages with additional launch modes expose a menu beside the launch action.
3. Open **Console** to follow startup. A running process can still be loading dependencies or models; wait for the package to report that it is ready.
4. For packages that expose a browser interface, click **Web UI** once it becomes available. This opens the address reported by the package.

Launching ComfyUI also makes it available to Stability Matrix's built-in **Inference** page. You can use that page or ComfyUI's own Web UI. See [Text to Image](../inference/text-to-image.md) for a first generation.

If startup fails, keep the relevant console output, including the first error and any traceback. The package console is separate from Stability Matrix's application log; [Common Issues](../troubleshooting/common-issues.md#finding-logs-and-reporting-bugs) explains where to find both.

## Stop and Restart

Use **Stop** on the running package's card to shut down its process. Closing a browser tab does not stop the package. Stop after the current generation or training job finishes unless you intend to interrupt it.

**Restart** stops and relaunches the package. Use it after saving launch options or changing settings that the package reads only at startup. Restarting a ComfyUI backend interrupts its connection to Inference until startup completes again.

## Update a Package

Updating Stability Matrix and updating an installed package are separate operations. An app update does not by itself select a new version of every installed tool.

1. Stop the package and record its current version if you may need to return to it.
2. Open the card's three-dot menu and choose **Check for Updates**.
3. When an update is available, click **Update** on the card and follow its progress.
4. Wait for completion, then launch the package and check the console.

Release installations use the package's release update path, including its prerelease setting. Branch installations update to the latest commit on their installed branch. Updates can also install Python dependencies, and the app may prompt for a Python upgrade when required by the package.

The menu's **Disable Update Check** option hides routine update checking for that installation. It does not create a backup or prevent you from deliberately changing versions.

## Change Versions

Use **three-dot menu → Change Version** to select another available release, branch, or commit, then confirm with **Update**. This is also the route for returning to a known version after an upstream change breaks a workflow.

A version change is not a full environment snapshot: dependencies and extensions can have changed too. If you need to preserve a working setup while trying another version, [install a second copy with a different display name](installing-packages.md#package-detail-view). Both installations can use the same shared model library.

## Per-Package Settings

The three-dot menu exposes the options supported by that package. Some actions are hidden for packages that do not support them.

| Action | What it controls |
|---|---|
| **Launch Options** | Startup flags for this installation; see [Launch Arguments](launch-arguments.md). |
| **Python Packages** | The installation's Python dependencies and available PyTorch indexes. |
| **Python Dependencies Override** | Dependency constraints used during installs and updates. |
| **Extensions** | The package's supported extension manager. |
| **Model Sharing** | Whether shared models are exposed through **Symlink**, **Config**, or **None**. |
| **Use Shared Output Folder** | Output sharing, where supported; independent of model sharing. |
| **Open in Explorer / Open in Finder** | Opens the package's installation folder. |

See [Shared Folders](../advanced/shared-folders.md) before changing storage options on an installation that already contains models or outputs.

## Uninstall a Package

Uninstalling removes the package's installation folder, including its Python environment, extensions or custom nodes, and custom files inside that folder.

Before proceeding, stop the package and check where its models and outputs actually live:

- Models in the shared model library outside the package folder remain available to other packages.
- **Config** sharing lets the package read shared models, but also permits real model files inside the package's own folders. Those local files are removed with the installation.
- With **None** selected, models in the package's folders are also removed.
- Outputs inside the installation are removed. Outputs already stored in the shared images directory outside it remain there.
- Save any package-local workflows, training data, configuration, or other custom files you want to keep elsewhere first.

Choose **three-dot menu → Uninstall**, review the deletion warning, and enter the displayed package name to confirm. The dialog's size estimate excludes symbolic-link targets; it is not the total size of your shared library.

Deleting a model from the Checkpoint Manager is a different operation: that removes the shared model itself. See [Checkpoint Manager](../checkpoint-manager/overview.md#rename-move-and-delete-models).

## Related Pages

- [Installing Packages](installing-packages.md)
- [Launch Arguments](launch-arguments.md)
- [Shared Folders](../advanced/shared-folders.md)
- [Common Issues](../troubleshooting/common-issues.md)
