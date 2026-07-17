# Hardware Support

Stability Matrix runs image and video generation packages on top of PyTorch, and PyTorch needs a compute backend that matches your GPU. This page breaks down which GPUs and platforms each backend targets, what Stability Matrix does automatically when it detects your hardware, the known caveats, and which packages expose each backend.

[`Section Overview`](overview.md) | [`Home`](../README.md)

## Table of Contents

- [How Backends Are Chosen](#how-backends-are-chosen)
- [NVIDIA (CUDA)](#nvidia-cuda)
- [AMD on Windows](#amd-on-windows)
- [AMD on Linux — AMD (ROCm)](#amd-rocm)
- [Intel (IPEX)](#intel-ipex)
- [Apple Silicon (MPS)](#apple-silicon-mps)
- [CPU Fallback](#cpu-fallback)
- [What's Next](#whats-next)

---

## How Backends Are Chosen

Each package declares the set of PyTorch backends it supports, and Stability Matrix pre-selects a recommended one from your detected hardware. The general order of preference is CUDA for NVIDIA, then ZLUDA (Windows AMD), then IPEX (Intel), then native ROCm (Linux AMD, or supported Windows AMD), then DirectML (Windows AMD), and finally CPU as a last resort. If a package does not support your detected GPU, the recommended default falls back to CPU.

The backend is chosen at install time from the **PyTorch Index** dropdown, and can be changed afterward from the package's **Python Packages** dialog. See [Selecting a Hardware Backend](../package-manager/installing-packages.md#selecting-a-hardware-backend) for where these options live in the UI.

The lists below describe what the code checks for. Because hardware detection works off GPU names and compute capability, treat any GPU model boundaries as guidance rather than a hard guarantee: some GPUs work with manual configuration even when a badge is not shown, and some edge-case cards may need extra setup.

## NVIDIA (CUDA)

- **Platforms:** Windows and Linux.
- **GPUs:** NVIDIA GPUs are detected by name (including Tesla-branded cards). Compute-capability thresholds the code reasons about include legacy GPUs (compute capability below 7.5, roughly pre-Turing), Ampere-or-newer (8.6 and up), and Blackwell (12.0 and up).
- **What Stability Matrix does automatically:**
  - Installs PyTorch from the CUDA index. The current default is the CUDA 13.0 wheel index (`cu130`); GPUs flagged as legacy NVIDIA fall back to an older CUDA 12.6 index (`cu126`) for ComfyUI installs.
  - For Windows systems with an Ampere-or-newer GPU, ComfyUI exposes an optional **Install Triton and SageAttention** command for faster attention.
  - `xformers` is added on CUDA (and ZLUDA) installs when a package requests it.
- **Caveats:**
  - The `cu130` wheels require an NVIDIA driver of version 580 or newer. ComfyUI checks the installed driver on launch and warns if it is older than 580.x while `cu130` torch is installed, suggesting either a driver update or manually downgrading to an older torch index such as `cu128`.
  - Turing (RTX 2000-series) or newer is the practical recommendation; older cards may still work but are treated as legacy.
- **Packages:** CUDA is the most broadly supported backend. Every inference package that lists a GPU backend supports CUDA, and CUDA-only packages include Fooocus, SimpleSDXL, ForgeClassic, FramePack, and the training tools (Kohya's GUI, OneTrainer, FluxGym, AI Toolkit).

## AMD on Windows

AMD support on Windows is the most involved case, because there are three different paths depending on your GPU and package: native ROCm, ZLUDA, and DirectML.

### Native ROCm

Stability Matrix can install AMD's native ROCm PyTorch on Windows using AMD's official multi-architecture wheels. This path is gated to a specific set of GPU architectures. The code recognizes the following `gfx` architectures as supported on Windows:

- **RDNA4** — `gfx120x` (e.g. RX 9070, RX 9060 families).
- **RDNA3 / RDNA3.5** — `gfx110x` (RDNA3 desktop and mobile) and `gfx115x` (RDNA3.5 APUs such as the 890M / 8060S / Z2 Extreme families).
- **Older architectures** — `gfx101x` (RDNA1) and `gfx103x` (RDNA2), plus Vega/GCN5 (`gfx900`, `gfx906`).

Architectures in the `gfx110x`, `gfx115x`, and `gfx120x` ranges are treated as "modern"; the rest are treated as "legacy" and use a more conservative attention path.

**What Stability Matrix does automatically:**

- When a supported AMD GPU is present on Windows, ROCm becomes the recommended backend for ROCm-capable packages.
- Torch is installed from AMD's ROCm multi-arch index (`repo.amd.com/rocm/whl-multi-arch/`) as device-specific wheels (`torch[device-gfxNNNN]`). Vega parts (`gfx900` / `gfx906`) pull from the 'TheRock' nightly multi-arch feed instead, since these architecture builds currently are only avaliable there instead of the stable production distribution stream.
- On modern architectures it applies a set of ROCm performance and attention environment variables at launch (MIOpen find-mode tuning, AOTriton experimental flash attention, `COMFYUI_ENABLE_MIOPEN`, and an allocator tuning string). AOTriton is excluded on the `gfx1152` / `gfx1153` APU architectures, which it does not yet support. Legacy architectures instead force a math SDP fallback. The full variable list and exactly which ones are auto-applied are documented in [Environment Variables](environment-variables.md#amd-and-rocm-variables).
- ComfyUI offers optional extra commands for supported AMD GPUs, including **Install Triton and SageAttention (ROCm)** (Sage Attention 1.x), **Install Flash Attention (ROCm)** (legacy architectures), an **Install ROCm Development SDK** step, and an **Install bitsandbytes (ROCm)** step for Python 3.12 environments.
- **Packages:** ComfyUI, Stable Diffusion WebUI Reforge, InvokeAI, SwarmUI, and Wan2GP are supported by this install path.
  > [!NOTE] While not managed by Stability Matrix for ROCm installs, SD.Next has Windows-native ROCm install when the "Rocm" Pytorch index is selected in Advanced Installation Options during initial package install and `--use-rocm` is set in launch options. This install path is internally handled by SD.Next itself and currently only supports RDNA2 dedicated GPUs, RDNA3, RDNA3.5, and RDNA4 GPUs.

**Caveats:**

- Windows AMD ROCm implementation for Stability Matrix is explicitly in an experimental state. While these package installations are generally in a working state, untested or edge-case issues and incompatibilities may appear. Stability Matrix prints a notice asking you to report issues to Stability Matrix first, since the setup may not be officially supported by the upstream package developers.
- Only the architectures listed above are eligible. If your AMD GPU is not on the list, the recommended default becomes ZLUDA or DirectML instead. For a more detailed list of compatible AMD GPU architectures, please refer to the ROCm/TheRock [GPU Support](`https://github.com/ROCm/TheRock/blob/main/SUPPORTED_GPUS.md#rocm-on-windows`) table.

### ZLUDA

ZLUDA is a CUDA-to-AMD translation layer used by dedicated AMD-on-Windows packages. It is recommended on Windows AMD systems that are not covered by native ROCm.

- **Packages:** ComfyUI-Zluda, Stable Diffusion WebUI AMDGPU Forge, and SD.Next (which lists ZLUDA among its backends).
- **What Stability Matrix does automatically:**
  - Installs the ZLUDA runtime along with the required HIP SDK prerequisite (HIP SDK 6.4) and, for ComfyUI-Zluda, Visual Studio Build Tools for C++. Torch itself is installed from the CUDA index, since ZLUDA translates CUDA calls.
  - ComfyUI-Zluda sets its own launch-time environment variables (`FLASH_ATTENTION_TRITON_AMD_ENABLE`, `MIOPEN_FIND_MODE`, `MIOPEN_LOG_LEVEL`, `ZLUDA_COMGR_LOG_LEVEL`, and a `TRITON_OVERRIDE_ARCH` derived from your GPU's `gfx` arch). See [Environment Variables](environment-variables.md#amd-and-rocm-variables) for details.
- **Caveats:**
  - Installing the HIP SDK and Build Tools may require administrator privileges and a reboot.
  - AMD GPUs below the RX 6800 may require additional manual setup (both ComfyUI-Zluda and AMDGPU Forge carry this disclaimer).
  - ZLUDA is generally faster than DirectML for supported operations but remains an experimental translation layer.

### DirectML

DirectML is Microsoft's cross-vendor GPU acceleration API and acts as the broadest-compatibility fallback on Windows.

- **GPUs:** AMD, Intel, and some NVIDIA GPUs on Windows.
- **What Stability Matrix does automatically:** Installs the `torch-directml` package instead of a CUDA/ROCm torch build. On a Windows AMD system with no ROCm-supported GPU, DirectML/ZLUDA is the fallback recommendation.
- **Caveats:** Broad compatibility, but generally slower than CUDA or native ROCm, and upstream DirectML development has largely stagnated. Where possible, native ROCm or ZLUDA is preferable for AMD GPUs.
- **Packages:** ComfyUI, SD.Next, SwarmUI, SDFX, Stable Diffusion WebUI DirectML, and Fooocus-MRE list DirectML support.

## AMD (ROCm)

On Linux, AMD GPUs use native ROCm directly, which is the mature AMD path.

- **Platform:** Linux only.
- **GPUs:** Native ROCm-capable AMD GPUs. Stability Matrix recommends ROCm when the system has an AMD GPU, no NVIDIA GPU, and is running Linux.
- **What Stability Matrix does automatically:**
  - Installs PyTorch from a ROCm wheel index. The default torch index is ROCm 6.4 (`rocm6.4`), and ComfyUI installs use a ROCm 7.2 index (`rocm7.2`).
  - Selects ROCm as the recommended backend automatically for ROCm-capable packages.
- **Caveats:**
  - Native ROCm on Linux depends on a system-level ROCm installation and a compatible kernel/driver stack, which Stability Matrix does not install for you.
  - The Windows-only ROCm performance environment overrides described above are not auto-applied on Linux, so if you want them you can set them yourself via the [Environment Variables](environment-variables.md) editor.
- **Packages:** ComfyUI, Stable Diffusion WebUI, SD.Next, Stable Diffusion WebUI Forge, InvokeAI, SwarmUI, SDFX, OneTrainer, and Wan2GP list ROCm support.

## Intel (IPEX)

IPEX is the Intel Extension for PyTorch, targeting Intel's discrete and integrated Arc graphics via the XPU backend.

- **Platforms:** Windows and Linux.
- **GPUs:** Intel Arc graphics. Note that detection is name-based and matches GPUs whose name contains "Arc", so Arc A-series and B-series discrete cards and Core Ultra parts with integrated Arc graphics are the intended targets.
- **What Stability Matrix does automatically:**
  - Installs PyTorch from Intel's XPU index (`xpu`).
  - Recommends IPEX when an Intel Arc GPU is detected and the package supports it.
  - For SD.Next, the Intel path runs the package's own `--use-ipex` install/launch flow.
- **Caveats:** Because detection keys off the "Arc" name, older non-Arc Intel integrated graphics are not recognized as IPEX-capable.
- **Packages:** ComfyUI and SD.Next list IPEX support.

## Apple Silicon (MPS)

MPS is Apple's Metal Performance Shaders backend, used for GPU acceleration on Apple Silicon Macs.

- **Platform:** macOS on Apple Silicon (arm64). M1 and newer.
- **What Stability Matrix does automatically:**
  - On macOS ARM, hardware compatibility always passes during first-launch setup, so the MPS path is offered without a discrete GPU check.
  - MPS is included with PyTorch on macOS and needs no separate compute-library download; the torch install uses the CPU wheel index, and PyTorch provides the Metal-backed device at runtime.
- **Caveats:** Support is specific to Apple Silicon; Intel Macs are not covered by this path. As with any backend, individual model or node compatibility can still vary.
- **Packages:** ComfyUI, Stable Diffusion WebUI, SD.Next, Stable Diffusion WebUI Forge, InvokeAI, SwarmUI, and SDFX list MPS support.

## CPU Fallback

CPU is the universal fallback that runs entirely on the processor with no GPU acceleration.

- **Platforms:** All.
- **What Stability Matrix does automatically:** Installs PyTorch from the CPU wheel index (`cpu`). When a package supports no backend that matches your detected hardware, the recommended default falls back to CPU.
- **Caveats:** CPU execution is dramatically slower than any GPU backend and is generally only suitable for testing, or for systems without a compatible GPU.
- **Packages:** Most inference WebUIs that support multiple backends include CPU (for example ComfyUI, Stable Diffusion WebUI, SD.Next, Stable Diffusion WebUI Forge, InvokeAI, SwarmUI, SDFX, and Fooocus-MRE).

## What's Next

- [Installing Packages](../package-manager/installing-packages.md) — Where the PyTorch backend is chosen during install
- [Environment Variables](environment-variables.md) — The full list of ROCm/HIP and PyTorch variables, and which ones Stability Matrix auto-applies
- [Supported Packages](../package-manager/supported-packages.md) — The full package list and their hardware badges
