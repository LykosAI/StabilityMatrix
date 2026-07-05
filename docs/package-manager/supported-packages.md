# Supported Packages

Stability Matrix supports a range of AI inference and training packages. Install any of them with a single click from the Package Manager.

[`Section Overview`](overview.md) | [`Home`](../README.md)

## Table of Contents

- [Inference Packages](#inference-packages)
- [Training Packages](#training-packages)
- [Legacy Packages](#legacy-packages)

---

## Inference Packages

Inference packages are used for generating images and video. They provide their own web UI or API that Stability Matrix launches and manages.

| Package | Description |
|---|---|
| **AUTOMATIC1111 Stable Diffusion WebUI** | The original Gradio-based web interface for Stable Diffusion. The `dev` branch is installed by default as it is in active development while the `main` branch has been in a stale state|
| **Stable Diffusion WebUI reForge** | A fast-moving Forge fork that tracks new functionality and newer model architectures quickly. Beyond Stable Diffusion, it supports a range of newer families such as FLUX, SD3, PixArt, Hunyuan, WAN video models, and other recent transformer-led pipelines. |
| **Stable Diffusion WebUI Forge - Neo** | An NVIDIA-focused Forge fork in rapid development, aimed at newer functionality, current model architectures, and a streamlined high-performance workflow. |
| **ComfyUI** | A powerful, node-based graph UI for building custom inference pipelines across a wide range of modern image and video models. It has grown into one of the most popular local AI frontends, and Stability Matrix's Inference UI is built to work alongside it through ComfyUI's API and workflow backend. |
| **ComfyUI-Zluda** | A Windows-only ComfyUI variant using ZLUDA as an alternative AMD path when ROCm is not the preferred option, including on some modern Radeon GPUs and older GPUs without practical ROCm support. Like standard ComfyUI, it remains compatible with Stability Matrix's Inference UI through the same ComfyUI backend approach. HIP 6.4 SDK only, Radeon GPUs below RX 6800 may require manual intervention post-install.  |
| **InvokeAI** | A professional-grade tool with a polished UI, canvas editor, and a comprehensive workflow system. |
| **SD.Next** | An all-in-one WebUI supporting a broad range of SD models, backends, and video generation. |
| **SwarmUI** | A dial-and-input-driven frontend for the ComfyUI backend installed in Stability Matrix, designed to make advanced workflows more accessible without requiring constant node-graph editing. Formerly known as StableSwarm, it was originally developed in-house at Stability AI and now continues as an independent project. It includes many built-in power-user features, broad support for current and newer model families, and direct access to ComfyUI's own graph web UI from within the SwarmUI interface when you want to drop down to backend-level workflow editing. |
| **Cogstudio** | A Gradio-based interface for generating and editing videos using CogVideoX models. |
| **Stable Diffusion Web UI (DirectML)** | A fork of the AUTOMATIC1111 WebUI with DirectML support for running on Windows without CUDA. |
| **FramePack** | An advanced next-frame-prediction neural network for progressively generating video content. |
| **FramePack Studio** | A full-featured video generation application built on top of the FramePack architecture. |
| **Wan2GP** | A highly optimized Gradio UI for AI video creation using WAN-based models, with performance-focused features and worfklows aimed at making modern and newer video-generation models more practical on lower-VRAM systems. |

## Training Packages

Training packages are used to fine-tune or train AI models such as LoRAs, checkpoints, and adapters.

| Package | Description |
|---|---|
| **AI-Toolkit** | An all-in-one training suite for diffusion models supporting LoRA, full fine-tune, and more. |
| **OneTrainer** | A comprehensive one-stop solution for Stable Diffusion model training with a graphical interface. |
| **kohya_ss** | A Windows-focused Gradio GUI wrapping Kohya's popular Stable Diffusion trainer scripts. Windows only.|
| **FluxGym** | A simple, low-VRAM Flux LoRA training UI designed for quick fine-tuning workflows. |

---

## Legacy Packages

The following packages are no longer actively maintained or have been superseded by newer alternatives. They remain available for installation but are not recommended for new setups.

- **Stable Diffusion WebUI Forge**
- **Stable Diffusion WebUI Forge - Classic**
- **Stable Diffusion WebUI AMDGPU Forge**
- **SDFX**
- **Fooocus, Fooocus-ControlNet, Fooocus-MRE**
- **RuinedFooocus**
- **Fooocus - mashb1t's 1-Up Edition**
- **Stable Diffusion Web UI-UX**
- **VoltaML**
