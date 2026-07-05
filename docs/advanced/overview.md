# Overview

The Advanced section covers the technical details behind how Stability Matrix works: how packages are built and run, how shared model storage and symlinks are organized, how hardware backends are selected, how the Python environment is managed, and how the app integrates with ComfyUI. These pages are aimed at users who want to understand or fine-tune the internals rather than just use the defaults.

[`Home`](../README.md)

---

Most users never need to touch these topics, since Stability Matrix handles environments, dependencies, and shared folders automatically. They are here for troubleshooting, customization, and for anyone building from source or contributing to the project.

## In This Section

- [Environment Variables](environment-variables.md) — Per-package environment variable configuration
- [ComfyUI Integration](comfyui-integration.md) — ComfyUI node API, WebSocket protocol, and custom nodes
- Building from Source and Contributing *(planned)* — Local builds, runtime targets, and where to start for code or docs contributions
- Shared Folders *(planned)* — Folder structure, symlinks, and cross-package model sharing
- Hardware Support *(planned)* — CUDA, ROCm, DirectML, MPS, ZLUDA, IPEX, and CPU backends
- Python Environment *(planned)* — Virtual environments, uv, pip, and Python version management
