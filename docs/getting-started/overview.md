# Overview

Stability Matrix is a free, open-source desktop application for installing, managing, and using local AI image and video generation tools. This page gives a high-level introduction to what the app does, what platforms it supports, and what kind of hardware is typically needed to run it well.

[`Home`](../README.md)

## Table of Contents

- [What is Stability Matrix?](#what-is-stability-matrix)
- [Key Features](#key-features)
- [Supported Platforms](#supported-platforms)
- [System Requirements](#system-requirements)
- [What's Next](#whats-next)

---

## What is Stability Matrix?

Stability Matrix is a desktop application that reduces the setup and maintenance work usually involved in running local AI generation tools. Instead of manually installing Python, cloning repositories, managing virtual environments, and sorting out model folders for each tool separately, you install and launch supported packages through a single interface.

Under the hood, Stability Matrix manages packages such as ComfyUI, Stable Diffusion WebUI, Forge-based WebUIs, InvokeAI, and other supported tools as isolated installations. At the same time, it lets them share common resources such as model storage, so you do not need to duplicate large checkpoints, VAEs, LoRAs, and other assets across every package.

It also adds features above those packages themselves, including the built-in Inference UI, unified model browsing, output management, update handling, and global configuration. The goal is not to replace every underlying tool, but to make them easier to install, organize, and use from one place.

## Key Features

Stability Matrix combines package management, model management, and generation workflows into a single desktop application. Its core feature set is designed to remove the repetitive setup work that normally comes with running multiple Stable Diffusion tools side by side.

- **One-click package management**: Install, update, launch, and remove supported packages from one interface. Stability Matrix handles the package repository, Python environment, embedded dependencies, and update flow so you do not have to maintain each tool manually.
- **Support for multiple ecosystems**: Use ComfyUI, Stable Diffusion WebUI variants, InvokeAI, training tools, and other supported packages from the same app. This makes it practical to compare tools, keep separate installs for different workflows, or run more than one package on the same system when resources allow.
- **Shared model library**: Store checkpoints, LoRAs, VAEs, ControlNet models, embeddings, upscalers, and other assets in one shared Models directory instead of duplicating them for every package. Importing a model once can make it available across the packages that support that model type.
- **Built-in Inference UI**: Generate images and video from Stability Matrix's native interface while using ComfyUI as the backend. The Inference UI provides structured panels, prompt editing tools, project tabs, saved `.smproj` workspaces, and a workflow that gives new users a quick path from installation to a first generation while still leaving room for more advanced controls as they learn the tool.
- **Integrated model discovery and downloads**: Browse and download models directly from sources such as CivitAI, HuggingFace, and OpenModelDB. Stability Matrix places downloads into the correct shared model folders, tracks progress, and preserves related metadata and preview images when available.
- **Outputs gallery and metadata-aware iteration**: Review generated images and video in a centralized gallery, inspect metadata, and send images back into inference workflows. This makes it easier to revisit earlier generations, compare results, and continue iterating without manually hunting through output folders.
- **Built-in launcher and runtime controls**: Start packages from a native launch page with real-time console output, configurable launch arguments, and environment variables. This helps with day-to-day use as well as troubleshooting, because you can monitor startup logs and open each package's own web UI once it is ready.
- **Extensions and customization**: Install extensions, plugins, or custom nodes for supported packages without leaving the app. Stability Matrix also exposes launch options, shared storage behavior, and advanced configuration so you can tailor each package to your system and workflow.
- **Portable, cross-platform workflow**: Stability Matrix is available on Windows, Linux, and macOS, and its data directory can be moved to another drive or system more easily than a hand-built setup. That makes it useful both for first-time local setup and for maintaining a larger long-term model library.

## Supported Platforms

Stability Matrix is cross-platform, but the exact release formats and hardware targets differ by operating system. The table below reflects the platforms that are documented and shipped by the project today.

| Operating System | Version / Target | Architecture | Notes |
|---|---|---|---|
| Windows | Windows 10 and Windows 11 | x64 | Official release builds are published for `win-x64`. This is the broadest-supported desktop target for Stability Matrix and most package workflows. |
| Linux | Modern x86-64 desktop distributions | x64 | Official Linux releases are published for `linux-x64`, primarily as an AppImage, with an AUR package also available for Arch-based systems. Depending on the distribution, you may need AppImage/runtime support packages such as `libfuse2`, `libappimage`, or `libxcrypt-compat` if they are not already provided by the system. |
| macOS | Apple Silicon Macs, with macOS 12.3 or later recommended for AI workflows | arm64 | Official macOS releases are published for Apple Silicon (`osx-arm64`) as a `.dmg`. The app's AI workflows rely on the MPS backend on Apple Silicon. |

In other words, the practical supported release targets are Windows x64, Linux x64, and Apple Silicon macOS. Some project files include additional runtime identifiers, but the documented source-build support and the release pipeline currently focus on `win-x64`, `linux-x64`, and `osx-arm64`. If you want to work from a local checkout instead of a packaged release, see [Building from Source and Contributing](../advanced/building-from-source.md) for the documentation entry point and links to the repository's contributor guide.

## System Requirements

Stability Matrix itself is distributed as a portable, self-contained desktop app, so you do not usually need to install Python, Git, or package managers separately. In practice, the real hardware requirements come from the packages, models, and workflows you want to run.

- **Operating system and architecture**: Use one of the supported desktop targets listed above: Windows x64, Linux x64, or Apple Silicon macOS.
- **GPU**: A dedicated GPU is strongly recommended for image and video generation. NVIDIA CUDA is the broadest and most mature path in current Stability Matrix workflows, with 900-series cards as a practical minimum and 2000-series or newer recommended for better compatibility and speed. AMD ROCm, AMD ZLUDA, Intel Arc (IPEX), and Apple Silicon (MPS) are also supported depending on platform.
- **VRAM**: Around 4 GB of VRAM is a practical minimum for older and lighter image-generation setups (Stable Diffusion 1.5), but 12+ GB is a better target for most current basic models and workflows (e.g. SDXL, zImage Turbo). Large modern models such as unquantized FLUX variants, and many video-generation workflows, can push that much higher. Lower-VRAM video variants may work in the 6-8 GB range, while larger video models can require 16+ GB.
- **System RAM**: 16GB recommended minimum. Requirements vary by backend and model size, but more system RAM becomes important when workloads spill out of VRAM. 32+ GB of RAM can help avoid hard out-of-memory crashes on constrained VRAM setups, even though performance will still slow down when offloading occurs. On memory-constrained systems, it also helps to make sure your page file on Windows or your swap file or swap partition on Linux is configured with enough space to act as a last-resort buffer when both VRAM and system RAM are exhausted.
- **Storage**: Plan for significant disk usage in the data directory. A single package install is typically in the 2-10 GB range, checkpoint models are often 2-20 GB each, and LoRAs or other adapters commonly range from tens of megabytes to around 1 GB each. An SSD is recommended for packages and active workflows, while slower bulk storage (HDD) can still be reasonable for large model libraries at the cost of initial model loading speed.
- **CPU-only fallback**: CPU-only operation is possible, but it is mainly useful for testing or very light use. For real generation workloads, it is much slower than any supported GPU backend.

If you are unsure what hardware target to optimize for, the safest general recommendation is a supported OS, a modern dedicated GPU, at least enough VRAM for your intended model family, and a storage drive with plenty of free space for packages, models, and outputs.

For a deeper breakdown of supported GPU backends, platform-specific acceleration paths, and hardware caveats, see [Hardware Support](../advanced/hardware-support.md).

## What's Next

- [Installation](installation.md) — Download and install Stability Matrix
- [First Launch](first-launch.md) — Complete the setup wizard
