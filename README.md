# Stability Matrix

[![Build](https://github.com/LykosAI/StabilityMatrix/actions/workflows/build.yml/badge.svg)](https://github.com/LykosAI/StabilityMatrix/actions/workflows/build.yml)
[![Discord Server](https://img.shields.io/discord/1115555685476868168?logo=discord&logoColor=white&label=Discord%20Server)](https://discord.com/invite/TUrgfECxHz)

[release]: https://github.com/LykosAI/StabilityMatrix/releases/latest
[download-win-x64]: https://github.com/LykosAI/StabilityMatrix/releases/latest/download/StabilityMatrix-win-x64.zip
[download-linux-x64]: https://github.com/LykosAI/StabilityMatrix/releases/latest/download/StabilityMatrix-linux-x64.zip
[download-macos]: https://github.com/LykosAI/StabilityMatrix/issues/45 

[auto1111]: https://github.com/AUTOMATIC1111/stable-diffusion-webui
[comfy]: https://github.com/comfyanonymous/ComfyUI
[sdnext]: https://github.com/vladmandic/automatic
[voltaml]: https://github.com/VoltaML/voltaML-fast-stable-diffusion
[invokeai]: https://github.com/invoke-ai/InvokeAI
[fooocus]: https://github.com/lllyasviel/Fooocus
[fooocus-mre]: https://github.com/MoonRide303/Fooocus-MRE

[civitai]: https://civitai.com/

Multi-Platform Package Manager for Stable Diffusion

### ✨ New in 2.5 - [Inference](#inference), a built-in Stable Diffusion interface powered by ComfyUI

### 🖱️ One click install and update for Stable Diffusion Web UI Packages
- Supports [Automatic 1111][auto1111], [Comfy UI][comfy], [SD.Next (Vladmandic)][sdnext], [VoltaML][voltaml], [InvokeAI][invokeai], [Fooocus][fooocus], and [Fooocus MRE][fooocus-mre]
- Embedded Git and Python dependencies, with no need for either to be globally installed
- Fully portable - move Stability Matrix's Data Directory to a new drive or computer at any time

### 🚀 Launcher with syntax highlighted terminal emulator, routed GUI input prompts
- Launch arguments editor with predefined or custom options for each Package install
- Configurable Environment Variables

### 🗃️ Checkpoint Manager, configured to be shared by all Package installs
- Option to find CivitAI metadata and preview thumbnails for new local imports

### ☁️ Model Browser to import from [CivitAI][civitai]
- Automatically imports to the associated model folder depending on the model type
- Downloads relevant metadata files and preview image

![header](https://cdn.lykos.ai/static/sm-banner-rounded.webp)

[![Release](https://img.shields.io/github/v/release/LykosAI/StabilityMatrix?label=Latest%20Release&link=https%3A%2F%2Fgithub.com%2FLykosAI%2FStabilityMatrix%2Freleases%2Flatest)][release]

[![Windows](https://img.shields.io/badge/Windows-%230079d5.svg?style=for-the-badge&logo=Windows%2011&logoColor=white)][download-win-x64]
[![Linux](https://img.shields.io/badge/Linux-FCC624?style=for-the-badge&logo=linux&logoColor=black)][download-linux-x64]
[![macOS](https://img.shields.io/badge/mac%20os%20%28apple%20silicon%29-000000?style=for-the-badge&logo=macos&logoColor=F0F0F0)][download-macos]

> macOS builds are currently pending: [#45][download-macos]

### Inference - A reimagined built-in Stable Diffusion experience
- Powerful auto-completion and syntax highlighting using a formal language grammar
- Workspaces open in tabs that save and load from `.smproj` project files

![](https://cdn.lykos.ai/static/sm-banner-inference-rounded.webp)

- Customizable dockable and float panels
- Generated images contain Inference Project, ComfyUI Nodes, and A1111-compatible metadata
- Drag and drop gallery images or files to load states

![](https://cdn.lykos.ai/static/sc-inference-drag-load-2.gif)

### Searchable launch options
<p align="center">
  <img style="width: 80%; height: 80%" src="https://github.com/LykosAI/StabilityMatrix/assets/13956642/75456866-9d95-47c6-8c0a-fdc19443ee02" alt=""/>
</p>

### Model browser powered by [Civit AI][civitai]
- Downloads new models, automatically uses the appropriate shared model directory
- Pause and resume downloads, even after closing the app

<p align="center">
  <img style="width: 80%; height: 80%" src="https://github.com/LykosAI/StabilityMatrix/assets/13956642/30b9f610-6033-4307-8d92-7d72b93cd73e" alt=""/>
</p>

### Shared model directory for all your packages
- Import local models by simple drag and drop
- Option to automatically find CivitAI metadata and preview thumbnails for new local imports

<p align="center">
  <img style="width: 80%; height: 80%" src="https://github.com/LykosAI/StabilityMatrix/assets/13956642/d42d1c53-67a4-45a0-b009-21400d44e17e" alt=""/>
</p>

- Find connected metadata for existing models
<p align="center">
  <img style="width: 80%; height: 80%" src="https://cdn.lykos.ai/static/sc-checkpoints-find-connected.gif" alt=""/>
</p>

## Localization
Stability Matrix is now available in the following languages, thanks to our community contributors:
- 🇺🇸 English
- 🇯🇵 日本語 
  - kgmkm_mkgm
- 🇨🇳 中文（简体，繁体）
  - jimlovewine
- 🇮🇹 Italiano
  - Marco Capelli
- 🇫🇷 Français
  - eephyne
- 🇪🇸 Español
  - Carlos Baena 
  - Lautaroturina

If you would like to contribute a translation, please create an issue or contact us on Discord. Include an email where we'll send an invite to our [POEditor](https://poeditor.com/) project.

## License

This repository maintains the latest source code release for Stability Matrix, and is licensed under the [GNU Affero General Public License](https://www.gnu.org/licenses/agpl-3.0.en.html). Binaries and executable releases are licensed under the [End User License Agreement](https://lykos.ai/license).
