# Installing Packages

This page walks through installing an WebUI package in Stability Matrix using the **Add Package** screen.

[`Section Overview`](overview.md) | [`Home`](../README.md)

## Table of Contents

- [The Add Package Screen](#the-add-package-screen)
- [Package Detail View](#package-detail-view)
- [Selecting a Version](#selecting-a-version)
- [Selecting a Hardware Backend](#selecting-a-hardware-backend)
- [Installation Progress](#installation-progress)
- [One-Click Install](#one-click-install)

---

## The Add Package Screen

The **Add Package** screen is where you discover and install new WebUI packages. To access it, navigate to **Packages** from the main navigation sidebar, then click the **Add Package** button across the bottom of the packages view.

Packages are displayed as a scrollable list of cards organized into three tabs above the package search bar: 

- **Inference**: Image/video generation tools such as ComfyUI, Stable Diffusion WebUI, and Fooocus.
- **Training**: Model fine-tuning and training tools such as Kohya's GUI and OneTrainer.
- **Legacy**: Older packages that are maintained for existing users but not recommended for new installations. May be stale and no longer receiving updates.

Each package card shows the package name, author, a short description, and a row of **hardware compatibility badges** indicating which PyTorch backends the package supports from the following types CUDA (NVIDIA), ROCm (AMD-native), DirectML, macOS (MPS), ZLUDA (AMD), IPEX (Intel), or CPU. Note that the absence of a particular hardware badge does not necessarily mean the package is incompatible, some packages may still be usable with manual configuration or community-provided workarounds. Within each tab, beginner-friendly packages appear first, followed by advanced tools in alphabetical order.

Use the tabs to switch between package types, or type in the search bar to filter the list by name in real time. Incompatible packages are hidden by default: enable *Show All Packages* to see packages that do not officially support your current hardware (e.g., CUDA-only packages on an AMD system).

## Package Detail View

Clicking a package card opens the **package detail screen**, where you configure your installation before proceeding. The left side shows a preview image of the package; the right side contains all configuration options.

The following fields are shown at the top of the screen:

- **License and GitHub links**: Buttons to view the package's license and open its source repository.
- **Version selector**: Choose between **Releases** (tagged GitHub releases) and **Branches** (development branches with optional commit selection). See [Selecting a Version](#selecting-a-version).
- **Display Name**: An editable field that sets both the display name and folder name for the installation. Defaults to the package's canonical name. Changing this allows installing multiple copies of the same package under different names. The full install path is shown below the field.

A *Duplicate Warning* banner appears if an installation with the same name already exists. Change the **Display Name** field to proceed.

### Advanced Options

The **Advanced Options** section is a collapsible panel containing settings that most users can leave at their defaults:

- **Model Sharing**: Controls how model directories are linked to the shared `Models/` library. Options include **Symlink** (recommended for most users), **Configuration** (uses the package's own config files to point to shared paths), and **None** (isolated model folders).
- **PyTorch Index**: Choose the PyTorch compute backend for your GPU. See [Selecting a Hardware Backend](#selecting-a-hardware-backend).
- **Output Sharing**: Enabled by default. When enabled, generated outputs are saved to the shared `Images/` directory rather than inside the package folder.
- **Python Version**: Select the Python version for the package's virtual environment from the versions available via Stability Matrix's internal `uv` utility. A green checkmark indicates versions already downloaded and cached locally. Typically it is recommended to leave set to what the package is configured in SM as default for compatibility/upstream recommendation and, recommended only to change if specifically needed before installing.

### Python Dependencies Override

The **Pip Override** section is a separate collapsible panel that lets you override specific Python package dependencies during installation and updates. It presents a data grid where each row defines an override with three fields:

- **Action**: **Update** to change a dependency's version or constraint, or **Remove** to exclude it entirely.
- **Name**: the pip package name of the dependency to override.
- **Constraint** and **Version**: the version specifier (e.g., `>=`, `==`, `!=`) and target version to pin.

This is useful when you need to force a specific version of a dependency to resolve a compatibility issue, or to remove a problematic package from the install. For example, you might pin `numpy==1.26.4` or remove an optional dependency that causes conflicts.

### Installing

The **Install** button sits at the bottom of the screen, below all configuration options. It is disabled until a valid configuration is selected (no duplicate name, all required fields populated). Click it to begin the installation pipeline described in [Installation Progress](#installation-progress).

## Selecting a Version

Stability Matrix offers two version selection modes, controlled by the **Branches/Release** tab toggle on the package detail screen:

### Release Mode (Recommended)

Select from the package's published GitHub releases. This is the default and recommended mode for most users:

- **Latest release** (default): installs whichever release tag is newest at the time of installation, excluding pre-releases.
- **Specific release**: choose any tagged release from the dropdown, including pre-release versions if available.

Releases represent tested, versioned snapshots of the package and are recommended for users who prioritize stability.

### Branch Mode

For packages that do not publish formal releases, or for users who need the latest development changes, switch to branch mode:

- **Branch select**: choose a branch from the package's Git repository (e.g., `main`, `master`, `dev`).
- **Commit select** (Advanced Options): pick a specific commit on the selected branch. Dropdown will list the latest 10 commit hashes, with the newest (HEAD) being first listed.
- **Custom commit** (Advanced Options): enter the full commit SHA manually.

Branch mode is useful for testing bleeding-edge features that have not yet been packaged in a release. Use it with the understanding that development branches may be unstable or contain breaking changes.

> **Tip:** Some packages disable release mode because they do not publish GitHub releases, or the Releases install path is currently incompatible/not configured with StabilityMatrix. In those cases, only branch mode is available.

## Selecting a Hardware Backend

The **PyTorch backend** determines which GPU acceleration library your package uses for computation. Stability Matrix detects your hardware and pre-selects the recommended backend, but you can override it from the dropdown on the package detail screen.

| Backend | Platform | GPU | Notes |
|---------|----------|-----|-------|
| **CUDA** | Windows, Linux | NVIDIA (GTX 900-series and newer) | Best performance and broadest compatibility. CUDA toolkit is bundled with PyTorch; no separate driver installation beyond standard NVIDIA drivers. Turing (RTX 2000-series) or newer recommended. |
| **ROCm** | Windows, Linux | AMD (select GPUs per platform) | Native AMD GPU acceleration. On Linux, requires system-level ROCm installation. On Windows, uses AMD's TheRock technical preview builds. See [Hardware Support](../advanced/hardware-support.md#amd-rocm) for per-chip compatibility. |
| **DirectML** | Windows | AMD, Intel, some NVIDIA | Microsoft's DirectML API. Broad compatibility but slower performance than CUDA or ROCm. Development is largely stagnant; consider native ROCm, or ZLUDA if need be, as an alternative for AMD GPUs. |
| **ZLUDA** | Windows | AMD (via CUDA translation layer) | Experimental CUDA-to-AMD translation layer. Used by the ComfyUI-Zluda, SD.Next, and AMDGPU Forge packages. Generally faster than DirectML for supported operations. |
| **IPEX** | Windows, Linux | Intel Arc (discrete and integrated) | Intel Extension for PyTorch. Requires Intel Arc GPU (A-series, B-series) or modern Intel Core Ultra with integrated Arc graphics. |
| **MPS** | macOS | Apple Silicon (M1 and newer) | Apple's Metal Performance Shaders backend. Included with PyTorch on macOS; no additional setup required. |
| **CPU** | All | None | Runs entirely on the CPU. Significantly slower than any GPU backend. Suitable only for testing or systems with no compatible GPU. |

The pre-selected backend is determined by Default GPU selected at First-Launch or in Default GPU setting, along with internal recommended Torch checks Stability Matrix determines based on detected hardware. If a package does not support your detected GPU, the recommended default will fall back to CPU.

> **Note:** The PyTorch backend is selected at install time, but can be changed afterward via the **Python Packages** dialog — accessible from the package's three-dot menu on the Packages screen. See [Python Environment Management](../advanced/python-environment.md#viewing-installed-python-packages).

For in-depth platform-specific guidance, including driver requirements and known caveats, see [Hardware Support](../advanced/hardware-support.md).

## Installation Progress

Once you click **Install**, Stability Matrix executes the installation as a sequence of discrete steps. A progress dialog appears, showing the current step and overall progress. The installation pipeline runs the following steps in order:

| Step | What Happens |
|------|--------------|
| **1. Mark as Installing** | The package name is added to the in-progress installs list so other operations can avoid conflicts with the installation directory. |
| **2. Setup Prerequisites** | Stability Matrix ensures that required tools (`git`, `uv`, and the target Python version) are available. Missing prerequisites are downloaded and unpacked automatically into the Stability Matrix data directory. No system-wide Python or Git installation is required. |
| **3. Download Package** | The package's Git repository is cloned from GitHub to the `Packages/<InstallName>` directory inside your library. The specific version (release tag, branch, or commit) selected on the detail screen is checked out. |
| **4. Unpack Site Customize** | A `sitecustomize.py` bootstrap script is placed in the virtual environment to ensure the package uses the correct Python path configuration at runtime. |
| **5. Install Package** | A Python virtual environment (`venv`) is created at `Packages/<InstallName>/venv/` using `uv`. Stability Matrix then installs the package's Python dependencies, including the selected PyTorch backend variant, by running `uv pip install` with the package's requirements file. This step typically takes the longest, as large PyTorch wheels are downloaded. |
| **6. Setup Model Folders** | Symbolic links or configuration files are created to connect the package's model directories (e.g., `models/stable-diffusion`, `models/VAE`) to the shared `Models/` library. |
| **7. Setup Output Sharing**  | If output sharing is enabled, the package's output directory is linked to the shared `Images/` folder. |
| **8. Register Package** | The installed package is saved to the settings file with its full metadata: version, backend, Python version, and configuration. It now appears in your **Installed Packages** list and is ready to launch. |

The progress dialog shows a real-time log of each step. If any step fails, the dialog reports the error, and you can inspect the full console output for troubleshooting.

### Typical Install Times

| Scenario | Approximate Time |
|----------|------------------|
| Fast connection, cached PyTorch wheels | 2–5 minutes |
| First install (no cached wheels) | 5–15 minutes |
| Slow connection or CPU-only install | 10–25 minutes |

> **Note:** PyTorch wheels are large and the multiple needed WHL files needed can accumulate to several GB's or more in total download size depending on backend used. The first installation on a fresh system downloads these wheels. Subsequent installs reuse cached wheels, making them significantly faster.

## One-Click Install

For new users, Stability Matrix offers a streamlined **one-click install** experience that appears on first launch. This guided flow installs a recommended package with sensible defaults, requiring no configuration decisions.

### How It Works

1. **Welcome dialog**: on the first launch after a fresh install, Stability Matrix presents a welcome screen with a brief explanation and a large **Install** button. The first compatible package that offers one-click installation is pre-selected; you can choose a different package from the dropdown if desired.

2. **Automatic configuration**: the one-click flow selects sensible defaults automatically:
   - The **latest release version** (or latest commit, for packages without releases).
   - The **recommended PyTorch backend** detected from your hardware.
   - The **recommended shared folder method** (symlinks for most packages).
   - The **package's recommended default Python version** 

3. **Installation**: clicking Install runs the same step pipeline described in [Installation Progress](#installation-progress). A progress bar shows the current status, and status text updates as each step completes.

4. **Post-install**: after successful installation, a brief countdown appears and the dialog closes, returning you to the Packages screen with the newly installed package ready to launch.

### Skipping One-Click Install

If you prefer to explore the full Add Package screen or already know which package you want, click the **Skip first-time setup** link at the bottom of the one-click dialog. This closes the dialog and leaves you at the main Package Manager interface.

### Re-accessing the One-Click Flow

The one-click install dialog is a first-launch experience only. Once dismissed or completed, it does not reappear. All subsequent package installations are done through the standard **Add Package** → package detail workflow described in the sections above.

## Next Steps

Once a package is installed, you can launch it, monitor its console output, configure launch arguments, run multiple packages simultaneously, update to newer versions, or remove it entirely. See [Managing Packages](managing-packages.md) for details on all of these workflows.
