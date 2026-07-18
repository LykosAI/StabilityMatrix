# Overview

The Package Manager is where Stability Matrix installs, updates, launches, and manages the AI generation packages you use, such as ComfyUI, Stable Diffusion WebUI variants, Forge, InvokeAI, and various training tools. This section of the documentation covers how those workflows work.

[`Home`](../README.md)

---

From the **Packages** screen you can install new packages through a guided flow, keep multiple packages side by side as isolated installations, launch them with live console output, update or roll back versions, and configure per-package options such as launch arguments, environment variables, shared model folders, and extensions. Each package keeps its own Python environment while sharing common resources like the model library, so the same checkpoints and LoRAs do not need to be duplicated for every tool.

## In This Section

- [Supported Packages](supported-packages.md) — Full list of supported inference and training packages
- [Installing Packages](installing-packages.md) — One-click install, hardware selection, and GPU backends
- Managing Packages *(planned)* — Launching, monitoring, updating, and deleting installed packages
- Launch Arguments *(planned)* — Configuring launch arguments per package
- Extensions *(planned)* — Browsing and managing package plugins and extensions

## What's Next

- [Installing Packages](installing-packages.md) — Install your first package
- [Supported Packages](supported-packages.md) — See what you can install
