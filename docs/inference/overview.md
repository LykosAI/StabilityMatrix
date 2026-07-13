# Inference Overview

The Inference page is Stability Matrix's built-in image and video generation interface, powered by ComfyUI under the hood. It provides a structured, panel-based UI as an alternative to using a web browser to control ComfyUI directly.

[`Home`](../README.md)

## Table of Contents

- [What is the Inference UI?](#what-is-the-inference-ui)
- [Getting Started with Inference](#getting-started-with-inference)
- [Generation Modes](#generation-modes)
- [Panel Layout](#panel-layout)
- [The Prompt Editor](#the-prompt-editor)
- [Project Files (.smproj)](#project-files-smproj)
- [Related Pages](#related-pages)

---

## What is the Inference UI?

The Inference UI is Stability Matrix's own native generation interface. It is not an embedded browser view of ComfyUI, and it does not expose the full node-graph editor directly. Instead, it provides a structured desktop workflow for common generation tasks such as text-to-image, image-to-image, upscaling, and video generation.

The Inference UI is designed to provide an approachable image and video generation workflow inside Stability Matrix while still exposing a useful range of advanced functionality. It allows users to begin generating without leaving the Stability Matrix window, learn the core workflow concepts in a more guided environment, and later move on to more direct package-specific WebUI usage if needed, or continue using the Inference UI as their primary long-term interface.

Under the hood, Stability Matrix builds a real ComfyUI prompt graph for each generation tab and sends it to a connected ComfyUI backend. The backend connection uses ComfyUI's API and WebSocket endpoints for prompt submission, progress updates, preview images, execution status, and output retrieval. Local input images are uploaded when needed, and generated outputs are saved back through Stability Matrix's own output and metadata pipeline.

In practical terms, the Inference UI is best understood as a curated control layer on top of ComfyUI. It covers the most common workflows with a more approachable panel-based interface, while still relying on ComfyUI as the execution engine.

For a deeper explanation of the backend relationship, including how Stability Matrix builds ComfyUI graphs, communicates with the API and WebSocket endpoints, and handles inputs and outputs, see [ComfyUI Integration](../advanced/comfyui-integration.md).

## Getting Started with Inference

Inference requires a compatible backend, typically the ComfyUI package installed through Stability Matrix. ComfyUI-Zluda is also a compatible backend package for use with AMD GPUs. When that package is launched from Stability Matrix, Inference detects the running package, waits for startup to complete, and then connects automatically. If no compatible backend is already running, the user can open a connection-help dialog that asks whether an installed ComfyUI backend should be launched and, when multiple compatible packages are installed, lets the user choose which one to start.

When the Inference UI opens with no previously restored tabs, Stability Matrix creates a new Text to Image tab automatically. Additional tabs can be created with the `+` button in the tab strip, which opens a mode picker for the supported generation types. Each tab is independent and can keep its own settings, prompts, and project file.

The Inference UI can reopen a previously saved project tab on startup, provided that tab was saved as an `.smproj` file and the file still exists. This restoration does not apply to unsaved tabs.

## Generation Modes

- **Text to Image** *(planned page)*: Creates images from prompts without a required source image. This is the default mode and the main entry point for most image-generation workflows.
- **Image to Image** *(planned page)*: Uses an input image together with prompt and sampler settings to guide edits, restyling, or controlled variation.
- **Image Upscale** *(planned page)*: Starts from an existing image and applies upscale methods exposed by the connected backend, including latent and model-based upscalers when available.
- **Wan Text to Video** *(planned page)*: Generates video from a text prompt using Wan video models.
- **Wan Image to Video** *(planned page)*: Generates video from a source image using Wan video models.
- **SVD Image to Video** *(planned page)*: Generates video from a source image using Stable Video Diffusion.

Video generation is not a single mode — it's split across three independent tabs, each its own project type, so Wan Text to Video, Wan Image to Video, and SVD Image to Video can be opened side by side with their own settings. All of these modes are implemented as separate tab view models, which is why different tabs can expose different cards, input requirements, and prompt behavior while still sharing the same backend connection.

## Panel Layout

Inference tabs use a dockable panel layout rather than a fixed single-column form. The exact arrangement varies by generation mode, but the default layout follows a consistent pattern.

Above the docked panels, the upper-right area of the Inference page contains page-level controls for backend connection and project actions. When no compatible backend is running, this area can show a `Launch` button that opens the connection-help flow for starting an installed ComfyUI backend. When a backend is already running or connected, the same area shows connection status controls instead.

That upper-right area also includes a three-dot menu for tab and project management actions. This menu provides commands for opening a project, saving the current project, saving the current project under a new name, and restoring the default layout.

On the left, most tabs expose configuration cards such as model selection, sampler settings, modules, seed controls, batch settings, or input-image selectors. In text-to-image, these controls appear as a stacked configuration pane. In certain image-based and video-based modes, Upscaler and SVD Image to Video, the left side may also include dedicated source-image cards.

The center area is typically reserved for the prompt workspace and the main generation controls. In text-to-image, this includes the prompt editor and a separate generate pane with the main generate, cancel, and seed-reuse actions. In other modes, the center layout may combine prompts with additional mode-specific controls such as source-image input.

On the right, the UI separates current-generation output from the output gallery pane. The main output pane shows generated previews, progress state, and current results, while a secondary gallery pane provides access to previously saved inference outputs and their metadata.

These panes are built on a dock layout system, so they can be resized, rearranged, hidden, and restored. The tab infrastructure also includes view-state save and restore hooks, which is why the layout behaves more like a workspace than a static form.

## The Prompt Editor

The prompt editor is a purpose-built text editor rather than a plain text box. It provides separate positive and negative prompt inputs, with the negative prompt pane enabled only in modes that use it.

Prompt text is parsed with Stability Matrix's prompt syntax system before generation. The editor supports common weighted-prompt constructs such as emphasis and deemphasis, as well as embeddings, LoRA and LyCORIS network tags, inline comments, and wildcard syntax. Prompt validation is performed before generation so invalid prompt syntax or unresolved extra networks can be surfaced clearly.

The editor also supports prompt-oriented tooling. Auto-completion can be enabled from settings, and the editor integrates completion and token-aware behaviors through the tokenizer and completion providers exposed by the app. Weight adjustment shortcuts are also built in: `Ctrl+Up` and `Ctrl+Down` for adjusting emphasis on the token under the caret or the current selection.

Beyond raw syntax entry, the prompt area can host prompt-related modules such as prompt expansion. The built-in Prompt Amplifier is also attached here, providing an optional assisted rewrite flow for supported accounts.

## Project Files (.smproj)

Inference tabs can be saved as `.smproj` files. These project files store the tab's project type together with a serialized state payload, which allows the tab to be reopened later in the correct mode.

In practice, an `.smproj` file captures the working state of the tab, including prompt content, model and sampler selections, seed and batch settings, enabled modules, and other mode-specific configuration. It records state, not model weights, so reopening a project still depends on the referenced models, extensions, and backend capabilities being available on the current system.

The Inference page supports standard project-style actions for these files, including Save, Save As, and Open, with keyboard shortcuts such as `Ctrl+S`, `Ctrl+Shift+S`, and `Ctrl+O`. New project files are saved through the app's project picker and default to the library `Projects` area rather than the ComfyUI workflows library.

Generated images can also carry Stability Matrix project metadata. When a saved output includes embedded Stability Matrix project data, dropping that image back onto a compatible Inference tab can restore the serialized state directly from the image metadata.

`.smproj` files are distinct from ComfyUI workflow JSON files. Project files capture the state of Stability Matrix's native Inference tabs, while the Workflows Browser *(planned page)* is for browsing and managing ComfyUI workflow files.

## Related Pages

- Text to Image *(planned)*
- Image to Image *(planned)*
- Image Upscale *(planned)*
- Video Generation *(planned)*
- Advanced Controls *(planned)*
- Outputs Overview *(planned)*
- [ComfyUI Integration](../advanced/comfyui-integration.md)
