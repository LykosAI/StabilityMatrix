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

Multi-Platform Package Manager for Stable Diffusion
- One click install and update for Stable Diffusion Web UIs
  - Currently supports [Automatic 1111][auto1111], [Comfy UI][comfy], [SD.Next (Vladmandic)][sdnext], with more planned
- Shared checkpoint manager with browsable imports from CivitAI

![header](https://github.com/LykosAI/StabilityMatrix/assets/13956642/a9c5f925-8561-49ba-855b-1b7bf57d7c0d)

[![Release](https://img.shields.io/github/v/release/LykosAI/StabilityMatrix?label=Latest%20Release&link=https%3A%2F%2Fgithub.com%2FLykosAI%2FStabilityMatrix%2Freleases%2Flatest)][release]

[![Windows](https://img.shields.io/badge/Windows-%230079d5.svg?style=for-the-badge&logo=Windows%2011&logoColor=white)][download-win-x64]
[![Linux](https://img.shields.io/badge/Linux-FCC624?style=for-the-badge&logo=linux&logoColor=black)][download-linux-x64]
[![macOS](https://img.shields.io/badge/mac%20os%20%28apple%20silicon%29-000000?style=for-the-badge&logo=macos&logoColor=F0F0F0)][download-macos]

> macOS builds are currently pending: [#45][download-macos]

## Features
- Launcher with syntax highlighted terminal emulator, launch arguments support
- Portable embedded dependencies and frameworks (like Git and Python) with no required changes to global installs.
- Virtual environment management using [Python.NET](https://github.com/pythonnet/pythonnet)

### Searchable launch options
<p align="center">
  <img style="width: 80%; height: 80%" src="https://raw.githubusercontent.com/LykosAI/lykosai.github.io/main/assets/images/screenshots/launchoptions.png" alt=""/>
</p>

### Model browser powered by [Civit AI](https://civitai.com/)

- Downloads new models, automatically uses the appropriate shared model directory
- Available immediately to all installed packages
<p align="center">
  <img style="width: 80%; height: 80%" src="https://raw.githubusercontent.com/LykosAI/lykosai.github.io/main/assets/images/screenshots/ckptbrowser.png" alt=""/>
</p>

### Shared model directory for all your packages

- Import local models by simple drag and drop
- Toggle visibility of categories like LoRA, VAE, CLIP, etc.
- For models imported from Civit AI, shows additional information like version, fp precision, and preview thumbnail on hover
<p align="center">
  <img style="width: 80%; height: 80%" src="https://raw.githubusercontent.com/LykosAI/lykosai.github.io/main/assets/images/screenshots/dragdropimport.gif" alt=""/>
</p>


## License

This repository maintains the latest source code release for Stability Matrix, and is licensed under the [GNU Affero General Public License](https://www.gnu.org/licenses/agpl-3.0.en.html). Binaries and executable releases are licensed under the [End User License Agreement](https://lykos.ai/license).
