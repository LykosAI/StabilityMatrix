# Common Issues

This page collects the problems that come up most often in Stability Matrix, organized by symptom: what you see, the likely cause, and what to try. It focuses on general fixes and safe first steps rather than deep per-package debugging.

[`Home`](../README.md)

## Table of Contents

- [Before You Start](#before-you-start)
- [Install Failures](#install-failures)
- [Launch and Update Failures](#launch-and-update-failures)
- [GPU and Backend Problems](#gpu-and-backend-problems)
- [Linux and macOS](#linux-and-macos)
- [Model Browser and CivitAI](#model-browser-and-civitai)
- [Inference Connection and Workflow Errors](#inference-connection-and-workflow-errors)
- [Finding Logs and Reporting Bugs](#finding-logs-and-reporting-bugs)
- [What's Next](#whats-next)

---

## Before You Start

Many issues clear up with a few quick steps before deeper troubleshooting:

- **Restart Stability Matrix**, and if a package is misbehaving, close and relaunch it.
- **Update Stability Matrix**, then update the affected package. A large share of reported problems are already fixed in a newer build.
- **Check free disk space** in your data directory. Package installs and model downloads need room to unpack, and low disk space is a common cause of failed installs.
- **Check antivirus quarantine.** Some antivirus suites quarantine or truncate files inside a package's virtual environment, which can break launches in ways that look like install corruption. If you suspect this, allow-list your data directory and reinstall.

If the problem persists, find the matching symptom below.

## Install Failures

**"Unable to install any package," or an install that fails partway through.**
This is usually an environment problem rather than a specific package bug: interrupted downloads, low disk space, a network timeout, or antivirus interference during the Python environment setup. Confirm you have free disk space, try again on a stable connection, and check antivirus quarantine as described above. If downloads are timing out, the pip and uv network variables (`PIP_TIMEOUT`, `UV_HTTP_TIMEOUT`, and the retry variables) documented in [Environment Variables](../advanced/environment-variables.md#common-environment-variables) can make installs more resilient on slow connections. The console output on the install page is the best place to see the underlying error.

**A package fails to start with a missing C/C++ runtime, for example an error loading PyTorch or a missing `c10.dll`.**
This is the Visual C++ Redistributable prerequisite. Stability Matrix normally installs it automatically, so this means the automatic step did not complete. The fallback (installing the redistributable manually) is covered in [Installation → Windows](../getting-started/installation.md#windows).

**Kohya's GUI fails with `No module named 'pkg_resources'` or `No module named 'packaging'`.**
These modules come from Python packaging tools (`setuptools` / `packaging`) that a training environment expects to be present before its own dependencies install. Stability Matrix pre-installs `packaging` and `setuptools` for Kohya's GUI, so if you hit this, first make sure Stability Matrix and the package are up to date, then reinstall the package so the environment is rebuilt cleanly.

**A ComfyUI install fails with `File not found: venv/uv-build-constraints.txt`.**
This is a known, reported class of issue tied to a build-constraints file that only resolves when the working directory is the install directory. Recent Stability Matrix builds explicitly avoid leaking that setting into the running server. If you see it, update Stability Matrix and reinstall or update ComfyUI; if it persists on the latest build, report it (see [Finding Logs and Reporting Bugs](#finding-logs-and-reporting-bugs)).

**Forge Neo fails to install or reinstall.**
Forge-based packages track fast-moving upstream repositories, and install failures here are frequently upstream dependency-resolution problems rather than a Stability Matrix bug. On Linux in particular, newer Python versions can make Torch resolution fail. As a first step, update Stability Matrix and retry the install; if it still fails, capture the console output and report it, since the exact cause tends to shift with upstream changes.

**You installed the CUDA backend but have an AMD GPU (or vice versa).**
The PyTorch backend is chosen at install time from the **PyTorch Index** dropdown and can be changed afterward. If the wrong one was selected, you generally do not need to reinstall from scratch: open the package's **Python Packages** dialog (from the package's three-dot menu on the Packages screen) and switch the PyTorch Index there. See [Selecting a Hardware Backend](../package-manager/installing-packages.md#selecting-a-hardware-backend) and [Hardware Support](../advanced/hardware-support.md) for which backend matches your hardware.

## Launch and Update Failures

**A package stops launching after updating Stability Matrix or Windows.**
Environment changes can leave a package's virtual environment in a stale state. Update the package so its environment is refreshed, and if that does not help, reinstalling the package rebuilds it cleanly while leaving your shared models and outputs intact.

**ComfyUI won't update or launch, is stuck on an old version, or reports "no update available."**
This is a recurring class of report rather than a single bug, and the cause varies (a pinned branch, a detached checkout, or a partially applied update). First confirm you are on the latest Stability Matrix build, then try updating the package again. If it stays stuck, reinstalling the package is the most reliable reset.

**`xformers` errors after an update.**
`xformers` is tightly coupled to specific PyTorch and CUDA versions, so an update on one side can break the pairing. Updating the package (which realigns the versions) usually resolves it. `xformers` is only added on CUDA and ZLUDA installs when a package requests it, as noted in [Hardware Support](../advanced/hardware-support.md).

**A package fails to launch with a `sitecustomize.py` `__main__.__file__` AttributeError on Windows.**
Stability Matrix writes a `sitecustomize.py` helper into each virtual environment, and this error is a known/reported class of issue in that area. Because that file loads on every interpreter startup, external software (some antivirus suites) truncating or corrupting it can also trigger startup failures. Update Stability Matrix so the current helper is written, check antivirus quarantine, and reinstall the package if the file remains damaged.

**Stability Matrix itself is slow to launch or does not launch after updating.**
First give it a moment on the first launch after an update, since some one-time setup runs then. If it still does not start, check antivirus quarantine of the application folder, and consult the application log described in [Finding Logs and Reporting Bugs](#finding-logs-and-reporting-bugs) for a startup error to report.

## GPU and Backend Problems

**Older NVIDIA cards (Pascal / GTX 10-series) fail with a PyTorch or CUDA error.**
Older GPUs may need an older PyTorch build than the current default. Stability Matrix treats legacy NVIDIA GPUs specially and can fall back to an older CUDA index for them, but if the auto-selected variant does not work, you can change the PyTorch Index from the **Python Packages** dialog. See [NVIDIA (CUDA)](../advanced/hardware-support.md#nvidia-cuda) for how legacy cards are handled.

**A new NVIDIA card errors on the newest CUDA build.**
The current default CUDA wheels require a recent NVIDIA driver. ComfyUI checks your driver on launch and warns if it is older than the required version, suggesting either a driver update or an older Torch index. Updating your NVIDIA driver is the usual fix; the driver-version detail is covered in [NVIDIA (CUDA)](../advanced/hardware-support.md#nvidia-cuda).

**AMD GPU on Windows: which backend should I use?**
AMD on Windows has three paths (native ROCm preview, ZLUDA, and DirectML), and the right one depends on your specific GPU. Rather than duplicate that here, see [AMD on Windows](../advanced/hardware-support.md#amd-on-windows) for the full breakdown and caveats.

**Your GPU is not recognized (for example the newest APUs).**
Hardware detection works from GPU names and compute capability, so very new or unusual parts can be missed. As noted in [Hardware Support](../advanced/hardware-support.md#how-backends-are-chosen), some GPUs still work with manual configuration even when no badge appears; you can set the PyTorch Index manually and test.

**`torch.cuda.OutOfMemoryError` or other out-of-memory errors during generation.**
This means the workload exceeded your available VRAM, not a Stability Matrix bug. Try a smaller image or batch size, a lower-VRAM model, or a VRAM-optimization flag if your package offers one. Tuning PyTorch's allocator via `PYTORCH_ALLOC_CONF` can help with fragmentation-related OOM; see [PyTorch and CUDA Variables](../advanced/environment-variables.md#pytorch-and-cuda-variables). The VRAM guidance in the [Overview](../getting-started/overview.md#system-requirements) is a useful sanity check for what a given model family needs.

## Linux and macOS

**The Linux AppImage will not run on some distributions.**
The AppImage may need runtime support packages such as `libfuse2` (and, depending on the distribution, ICU or related libraries) that are not installed by default. This and the `.desktop` and AUR quirks are covered in [Installation → Linux](../getting-started/installation.md#linux).

**macOS Gatekeeper blocks the first launch.**
This is expected for a downloaded app. The steps to allow it are in [Installation → macOS](../getting-started/installation.md#macos).

## Model Browser and CivitAI

**Search results are missing, or model metadata does not update.**
This is a known ecosystem issue rather than a fault in Stability Matrix. The CivitAI API has changed over time, and some content (notably certain NSFW material) has moved off the public API, so results can differ from the CivitAI website. There is often nothing to fix on the Stability Matrix side beyond keeping it updated; if a specific search or download consistently fails on the latest build, report it with details.

**Selecting a model card feels laggy.**
Large browse results with many previews can make selection feel slow. Narrowing your search or letting the current page finish loading generally helps.

## Inference Connection and Workflow Errors

**A generation is rejected with a 400 error such as a node not found.**
This means the workflow needs a ComfyUI custom node or extension that is not installed. When a workflow's required extensions are missing or out of date, Stability Matrix shows an **Install Required Extensions?** dialog listing them; accepting it installs the missing extensions and restarts the package before generating. If you cancel that prompt, the generation cannot run until the extensions are installed.

**Inference cannot connect, or connection is refused.**
Inference talks to a local ComfyUI server, so this usually means the backend is not running, or it is running on a non-default host or port. Stability Matrix reads the package's launch arguments and connects to the host and port there, falling back to `127.0.0.1:8188` when none are set. If you set a custom `--port` or `--listen`, make sure the package is actually launched and that those launch arguments are configured on the same package Inference is pointed at.

## Finding Logs and Reporting Bugs

Two kinds of logs are useful when something goes wrong:

- **The Stability Matrix application log.** This is written to `app.log` in the `Logs` folder under Stability Matrix's application data directory (on Windows, `%AppData%\StabilityMatrix\Logs`). The quickest way to open it is the **Logs** shortcut under the directory shortcuts in `Settings`, which opens that folder directly.
- **Package console output.** Each package's own startup and runtime output appears in its console view, reached with the **Console** action on the package. This is where install and launch errors from the underlying tool are shown, and it is the best place to copy an error message from.

When reporting a bug on the [GitHub issue tracker](https://github.com/LykosAI/StabilityMatrix/issues), including the following makes it far easier to help:

- Your Stability Matrix version and operating system.
- Your GPU (and, on AMD/Intel, which backend you selected).
- The affected package and its version.
- A relevant excerpt from the application log or the package console output.

For community help and quick questions, the project also has a Discord server, linked from the [project README](https://github.com/LykosAI/StabilityMatrix#readme).

> **Note on known bugs:** Some issues above are known/reported classes of problem without a guaranteed user-side fix. For those, the most reliable steps are to update Stability Matrix, update or reinstall the affected package, and — if it persists on the latest build — report it on the issue tracker with logs so it can be investigated.

## What's Next

- [Hardware Support](../advanced/hardware-support.md) — Which GPU backend matches your hardware, and how backends are chosen
- [Environment Variables](../advanced/environment-variables.md) — Network, cache, and PyTorch variables useful when troubleshooting installs and GPU behavior
- [Installing Packages](../package-manager/installing-packages.md) — Where the PyTorch backend and Python Packages dialog live
