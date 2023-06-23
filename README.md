# Stability Matrix

[![Build](https://github.com/LykosAI/StabilityMatrix/actions/workflows/build.yml/badge.svg)](https://github.com/LykosAI/StabilityMatrix/actions/workflows/build.yml)

![https://discord.com/invite/TUrgfECxHz](https://img.shields.io/discord/1115555685476868168?logo=discord&logoColor=white&label=Discord%20Server)

[auto1111]: https://github.com/AUTOMATIC1111/stable-diffusion-webui
[comfy]: https://github.com/comfyanonymous/ComfyUI
[sdnext]: https://github.com/vladmandic/automatic

An easy to use, powerful package manager for Stable Diffusion Web UIs, and managing model checkpoints.

Latest Release: [v1.0.0](https://github.com/LykosAI/StabilityMatrix/releases/latest)

## Features
- One click install and update for Stable Diffusion Web UIs, no dependencies needed
  - Currently supports [Automatic 1111][auto1111], [Comfy UI][comfy], [SD.Next][sdnext]
- Launcher with virtual environment management using [Python.NET](https://github.com/pythonnet/pythonnet)

<p align="center">
  <img style="width: 75%; height: 75%" src="https://raw.githubusercontent.com/LykosAI/lykosai.github.io/main/assets/images/screenshots/darkmode.png" alt="">
</p>

### Searchable launch options
<p align="center">
  <img style="width: 80%; height: 80%" src="https://raw.githubusercontent.com/LykosAI/lykosai.github.io/main/assets/images/screenshots/launchoptions.png" width="100" />
</p>

### Model browser powered by [Civit AI](https://civitai.com/)

- Downloads new models, automatically uses the appropriate shared model directory
- Available immediately to all installed packages
<p align="center">
  <img style="width: 80%; height: 80%" src="https://raw.githubusercontent.com/LykosAI/lykosai.github.io/main/assets/images/screenshots/ckptbrowser.png" alt="">
</p>

### Shared model directory for all your packages

- Import local models by simple drag and drop
- Toggle visibility of categories like LoRA, VAE, CLIP, etc.
- For models imported from Civit AI, shows additional information like version, fp precision, and preview thumbnail on hover
<p align="center">
  <img style="width: 80%; height: 80%" src="https://raw.githubusercontent.com/LykosAI/lykosai.github.io/main/assets/images/screenshots/dragdropimport.gif" alt="">
</p>


## License

This repository maintains the latest source code release for Stability Matrix, and is licensed under the [GNU Affero General Public License](https://www.gnu.org/licenses/agpl-3.0.en.html). Binaries and executable releases are licensed under the [End User License Agreement](https://lykos.ai/license).
