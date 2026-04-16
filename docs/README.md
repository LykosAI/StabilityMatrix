# Stability Matrix Documentation

Stability Matrix is a multi-platform package manager for Stable Diffusion and related AI image/video generation tools. This documentation covers all major features and sections of the application.

This docuemtation is intended to provide a detailed guide and explaination of the many functions of Stability Matrix, its installation and use for both new and current users, and also more detailed and technical material for advanced users.
While it cotains information on a vast majority of application specific functions, It also contains information that applies to AI image, video, and related generation aspects that can be useful both inside and outside of Stability Matrix.
While not all encompassing on every minute detail, it is intended to be updated as new features and changes are released to the project as well as new ecosystem/model/usage information as-needed.

Current Status: In-progress - Structure is in-place and planned docs are currently being progressively created and added.

## Table of Contents

### Getting Started
- [Overview](getting-started/overview.md) — What Stability Matrix is and what it can do and minimal requirements/recommendations on hardware
- [Installation](getting-started/installation.md) — Installing on Windows, macOS, and Linux
- [First Launch](getting-started/first-launch.md) — Walking through the setup wizard
- [Data Directory](getting-started/data-directory.md) — Choosing and managing your data directory

### Package Manager
- [Overview](package-manager/overview.md) — Managing AI packages in Stability Matrix
- [Supported Packages](package-manager/supported-packages.md) — Full list of supported inference and training packages
- [Installing Packages](package-manager/installing-packages.md) — One-click install, hardware selection, GPU backends
- [Managing Packages](package-manager/managing-packages.md) — Launching, monitoring, updating, and deleting installed packages
- [Launch Arguments](package-manager/launch-arguments.md) — Configuring launch arguments per package
- [Extensions](package-manager/extensions.md) — Browsing and managing package plugins and extensions

### Inference
- [Overview](inference/overview.md) — The Inference UI, panel layout, and project files
- [Text to Image](inference/text-to-image.md) — Generating images from text prompts
- [Image to Image](inference/image-to-image.md) — Using an image as a generation starting point
- [Image Upscale](inference/image-upscale.md) — Upscaling images with AI upscaler models
- [Video Generation](inference/video-generation.md) — Generating video with WAN and SVD models
- [Advanced Controls](inference/advanced-controls.md) — ControlNet, FaceDetailer, FreeU, LayerDiffuse, and more
- [Saving Projects](inference/saving-projects.md) — Saving and loading `.smproj` project files

### Checkpoint Manager
- [Overview](checkpoint-manager/overview.md) — Centralized model storage shared across all packages
- [Model Categories](checkpoint-manager/model-categories.md) — All supported model folder types explained
- [Metadata Editing](checkpoint-manager/metadata-editing.md) — Importing CivitAI metadata and editing model info

### Model Browser
- [Overview](model-browser/overview.md) — Multi-source model browser and download queue
- [CivitAI](model-browser/civitai.md) — Browsing and downloading from CivitAI
- [HuggingFace](model-browser/huggingface.md) — Browsing and downloading from HuggingFace
- [OpenModelDB](model-browser/openmodeldb.md) — Browsing upscaler models from OpenModelDB

### Outputs
- [Overview](outputs/overview.md) — Image gallery, sorting, filtering, and batch operations
- [Image Metadata](outputs/image-metadata.md) — Reading embedded generation parameters and ComfyUI node data

### Workflows
- [Overview](workflows/overview.md) — Browsing and managing ComfyUI workflows
- [Community Workflows](workflows/community-workflows.md) — Browsing community workflows via OpenArt

### Settings
- [Overview](settings/overview.md) — Navigating the settings hub
- [General](settings/general.md) — Theme, language, data directory, and shared folder settings
- [Accounts](settings/accounts.md) — Lykos account, OAuth login, and API tokens
- [Inference Settings](settings/inference-settings.md) — Inference UI behavior and defaults
- [Updates](settings/updates.md) — Auto-update channel and frequency settings

### Advanced
- [Building from Source and Contributing](advanced/building-from-source.md) — Local builds, runtime targets, and where to start for code or docs contributions
- [Shared Folders](advanced/shared-folders.md) — Folder structure, symlinks, and cross-package model sharing
- [Hardware Support](advanced/hardware-support.md) — CUDA, ROCm, DirectML, MPS, ZLUDA, IPEX, and CPU backends
- [Python Environment](advanced/python-environment.md) — Virtual environments, uv, pip, and Python version management
- [ComfyUI Integration](advanced/comfyui-integration.md) — ComfyUI node API, WebSocket protocol, and custom nodes
- [Environment Variables](advanced/environment-variables.md) — Per-package environment variable configuration

### Tips and Tricks
- [Overview](tips/overview.md) — Tips and Tricks index
- [Terminology](tips/terminology.md) — Common image generation terms and what they mean
- [Inference UI Tips](tips/inference-ui.md) — Effective use of the built-in Inference UI
- [Per-Package Tips](tips/per-package.md) — Package-specific tips and links to upstream documentation
- [AMD GPU Workflow](tips/amd-gpu-workflow.md) — Getting image and video generation working on AMD hardware
- [Model Dependencies](tips/model-dependencies.md) — Required secondary files for modern models (text encoders, VAEs, etc.)
- [VRAM Optimization](tips/vram-optimization.md) — Reducing VRAM usage without sacrificing too much quality or speed
