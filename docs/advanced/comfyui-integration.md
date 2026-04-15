# ComfyUI Integration

The Stability Matrix Inference UI is built on top of ComfyUI's API and WebSocket protocol. Understanding this integration is useful if you want to use ComfyUI's own web interface, use the API directly, or troubleshoot connection issues.

[`Section Overview`](overview.md) | [`Home`](../README.md)

## Table of Contents

- [How Stability Matrix Uses ComfyUI](#how-stability-matrix-uses-comfyui)
- [ComfyUI as a Standalone Package](#comfyui-as-a-standalone-package)
- [The ComfyUI Web Interface](#the-comfyui-web-interface)
- [Custom Nodes](#custom-nodes)
- [The ComfyUI API and WebSocket](#the-comfyui-api-and-websocket)

---

## How Stability Matrix Uses ComfyUI

The Inference UI does not replace ComfyUI. It uses ComfyUI as its execution backend.

When you configure a generation tab in Stability Matrix, the app builds a real ComfyUI prompt graph behind the scenes. The inference view models and modules assemble that graph with a node builder, adding the same kinds of nodes you would use manually in ComfyUI: model loaders, text encoders, samplers, VAE encode/decode steps, ControlNet preprocessors, image loaders, tiled VAE nodes, video nodes, and output preview nodes.

In practical terms, that means:

- every generation from the Inference UI is sent to ComfyUI as workflow-style node JSON
- local input images are uploaded into ComfyUI's input area before execution when needed
- some auxiliary files are copied into the local ComfyUI package directory when the workflow requires them
- progress, active-node changes, and preview images are streamed back while the workflow is running
- final outputs are fetched from ComfyUI after execution finishes, then saved by Stability Matrix with its own project and parameter metadata layered on top

So the relationship is best understood as: Stability Matrix provides a curated native UI, while ComfyUI is the engine actually running the graph.

The reverse is not fully symmetrical. Everything the Inference UI does is representable as a ComfyUI workflow, but not every arbitrary ComfyUI workflow is exposed through the Inference UI's built-in cards and panels.

## ComfyUI as a Standalone Package

ComfyUI is available as an installable package in Stability Matrix's Package Manager. When you launch that package through Stability Matrix, the Inference UI connects to it as its local backend.

By default, the ComfyUI package uses host `127.0.0.1` and port `8188`, and the Inference client connects to `http://127.0.0.1:8188`. If you change the ComfyUI launch arguments inside Stability Matrix, the Inference UI reads those host and port values and connects to the configured address instead.

This matters because the Inference UI is not tied to a hardcoded browser tab or embedded widget. It is a client talking to a running ComfyUI server. If the package is stopped, the Inference UI loses its backend. If the package is restarted, the Inference UI reconnects to that backend.

Stability Matrix also knows when it is talking to a locally managed ComfyUI install. In that case it can:

- upload inputs directly to the local package's `input` folder when needed
- read outputs from the local `output` directory
- manage custom-node installs through the package extension system
- restart the package after extension changes when necessary

## The ComfyUI Web Interface

The Inference UI and the ComfyUI web interface are meant to coexist.

Once the ComfyUI package is running, you can open ComfyUI's own browser-based node graph from the Launch page. That graph editor gives you direct access to the raw workflow layer, which is useful when you want to:

- inspect or modify workflows beyond what the Inference UI currently exposes
- use custom nodes or advanced graph structures that do not have native Inference UI cards
- import or build community workflows directly in ComfyUI
- debug a workflow at the node level

This is one of the main strengths of the integration: you can start with Stability Matrix's simpler native controls, then move into ComfyUI's graph editor when you need lower-level control.

Community and exported ComfyUI workflows are also commonly shared as JSON files. Those can be opened in ComfyUI directly, and they are often the easiest way to exchange complex graphs with other users.

## Custom Nodes

ComfyUI's functionality can be extended with custom nodes, and Stability Matrix is aware of that extension model.

For ComfyUI packages installed through Stability Matrix, custom nodes live in the `custom_nodes` directory. You can install them in two common ways:

- use the Extensions browser in Stability Matrix for the ComfyUI package
- manually clone a node repository into `custom_nodes`

Stability Matrix's ComfyUI package uses extension manifests for custom-node discovery. On new ComfyUI installs, ComfyUI Manager is also installed automatically through the package setup process, and the accompanying `--enable-manager` launch argument is enabled by default. That gives the ComfyUI side an in-browser extension manager out of the box.

There is also a deeper integration point in the Inference UI itself: when a built workflow declares required Comfy extensions, Stability Matrix checks whether those extensions are already installed before the first batch runs. If required extensions are missing or version-constrained extensions are out of date, Stability Matrix can prompt you to install or update them and then restart the ComfyUI package so the changes take effect.

This does not eliminate the need to understand custom nodes, but it does reduce some of the manual work when a workflow depends on specific ComfyUI extensions.

## The ComfyUI API and WebSocket

For advanced users, the integration is straightforward: Stability Matrix talks to ComfyUI over its normal local API and websocket endpoints.

The main pieces used by Stability Matrix are:

- REST base address: `http://127.0.0.1:8188/` by default
- `POST /prompt` to submit the generated node graph for execution
- `POST /interrupt` to cancel a running generation
- `POST /upload/image` to send input images into ComfyUI
- `GET /history/{promptId}` to retrieve executed outputs after the prompt finishes
- `GET /view` to download output images returned by ComfyUI history
- WebSocket at `ws://127.0.0.1:8188/ws?clientId=...` for live status, running-node changes, progress data, execution errors, and preview-image bytes

In practice, the websocket is what makes the Inference UI feel live. It is how Stability Matrix receives step progress, node execution state, and preview frames while ComfyUI is still working.

The REST side is used for the request-response parts of the workflow:

- upload inputs
- queue the prompt
- interrupt if cancelled
- fetch history and outputs when execution completes

If you are troubleshooting connection issues, those are the paths to keep in mind. If you are building your own automation around a local ComfyUI instance, Stability Matrix is effectively using the same public backend surface that advanced users can script against themselves.
