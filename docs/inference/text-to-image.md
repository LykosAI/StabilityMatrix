# Text to Image

This guide walks from an installed ComfyUI backend and a local model to a generated image in Stability Matrix's built-in **Inference** page.

[`Section Overview`](overview.md) | [`Home`](../README.md)

## Table of Contents

- [Before You Start](#before-you-start)
- [Generate Your First Image](#generate-your-first-image)
- [Seeds and Batches](#seeds-and-batches)
- [Save and Reopen Your Work](#save-and-reopen-your-work)
- [When Generation Does Not Work](#when-generation-does-not-work)
- [Related Pages](#related-pages)

---

## Before You Start

You need:

- A compatible ComfyUI backend installed through Stability Matrix. See [Installing Packages](../package-manager/installing-packages.md).
- A supported image model and any companion files it requires. Import local files through the [Checkpoint Manager](../checkpoint-manager/overview.md#import-local-models), or use a model downloaded during first-time setup.
- Enough memory for that model and workflow. Package installation alone does not include every model required for generation.

The steps below use a full image checkpoint, such as a conventional SD 1.5 or SDXL checkpoint, to avoid starting with a workflow that requires separately selecting several model components. A LoRA is an addition to a base model, not a replacement checkpoint. Standalone diffusion models can require separate text encoders and a VAE; follow the Model card's workflow and missing-companion guidance for those models.

## Generate Your First Image

### 1. Connect the backend

Launch your ComfyUI installation from **Packages** and wait for startup to finish. Open **Inference** and check its connection status. If no compatible backend is running, the page's **Launch** action opens a dialog to start one; choose the intended installation if you have several.

An empty workspace creates a **Text to Image** tab automatically. Otherwise, use the tab strip's **+** button to create a Text to Image tab. Starting a new tab avoids carrying settings over from a different project.

### 2. Choose the model

Use the **Model** card's picker to select your full checkpoint. If the picker is empty, confirm the backend connection, local model location, and package sharing before continuing.

The Model card can expose a **Workflow** selector and model-specific settings. Resolve any model placement or missing-companion notice. When a workflow offers recommended defaults, its defaults action applies sampler, scheduler, steps, CFG scale, and any supplied shift value. It does not set image dimensions for you.

For a first full-checkpoint generation, leave optional refiner, extra networks, and other advanced overrides disabled unless your selected model requires them. Leave Hires Fix and upscaling modules disabled while checking the basic generation path.

### 3. Enter a prompt

Start with a simple booru-style positive prompt: a comma-separated list of tags describing the scene.

```text
scenery, outdoors, cabin, lake, mountains, forest, pine tree, sunrise, reflection, watercolor
```

Follow your checkpoint's usage instructions for its preferred prompt style and any model-specific quality or trigger tags. If its examples use natural-language descriptions, use that style instead.

For a workflow with a negative prompt, you can initially leave it empty. Add exclusions later when you have a result to compare. This example does not require LoRA, embedding, or wildcard files.

### 4. Check the sampler and batch settings

Set **Batch Size** and **Batch Count** to `1`. Keep **Batch Index** disabled so you receive the full batch.

Choose dimensions and sampling settings appropriate to your model. A new sampler card starts at `1024 × 1024`, `20` steps, CFG scale `5`, Euler Ancestral, and the Normal scheduler in this checkout. These are UI defaults, not a preset suitable for every model. Model-specific workflow defaults or the model's supplied usage instructions can require different values, particularly for distilled or accelerated models.

Keep seed randomization enabled for this first run.

### 5. Generate and inspect the result

Click **Generate**. The app validates prompts and model selections before submitting work. If a required-extension dialog appears, review it and allow installation if you want to run that workflow; the backend may restart before generation continues.

Watch the output pane for progress and results. A first generation can take longer while the backend loads model weights. Use the package's **Console** if progress stops or an error appears. **Cancel** interrupts generation; it does not uninstall or stop the backend package.

By default, completed images are saved through the Inference output pipeline under `Images/Inference` in the data directory and appear in the Inference gallery. Optional output modules can add other saves.

## Seeds and Batches

The seed controls the random starting point. The seed card randomizes on each run by default. Toggle randomization off to lock it; the tooltip changes to **Seed is locked**. Lock the seed when comparing a prompt or setting change, keeping other settings the same.

| Control | Effect |
|---|---|
| **Batch Size** | Images in one generation batch. Larger batches can require more memory. |
| **Batch Count** | Number of batches requested by one Generate action. Text to Image increments the starting seed for each successive batch. |
| **Batch Index** | Optionally selects an image from the batch; leave it disabled when you want all images. |

A fixed seed helps comparisons within the same setup, but does not guarantee identical images across different models, backend versions, hardware, or extensions.

## Save and Reopen Your Work

Use the Inference page's three-dot menu to **Save** or **Save As** an `.smproj` project. Open it later through the same menu. A project preserves the tab's prompts, model selections, settings, and modules; it does not bundle model weights or install dependencies.

Generated images with embedded Stability Matrix project metadata can also restore state when dropped onto a compatible Inference tab. Use an original output file: an image editor or sharing service may strip its metadata. An ordinary image without that project data cannot restore the complete setup.

For file shortcuts, startup restoration, and layout controls, see [Inference Overview](overview.md#project-files-smproj).

## When Generation Does Not Work

| Symptom | Next step |
|---|---|
| No backend connection | Launch the intended ComfyUI installation and check its console. Review its [Host and Port options](../package-manager/launch-arguments.md#example-change-the-comfyui-port) if customized. |
| Model missing from the picker | Follow [A Model Is Missing](../checkpoint-manager/overview.md#a-model-is-missing), including sharing and backend refresh. |
| Missing text encoder or VAE | Supply compatible companions for the selected workflow, or select a full checkpoint for this walkthrough. |
| Prompt validation fails | Remove unresolved network tags or wildcard references and try the plain example prompt. |
| Missing or outdated custom node | Follow the required-extension prompt and let the backend restart. |
| Out of memory | Return to batch size `1`, reduce dimensions, and disable extra processing modules; consult [Common Issues](../troubleshooting/common-issues.md#gpu-and-backend-problems). |

## Related Pages

- [Inference Overview](overview.md)
- [Checkpoint Manager](../checkpoint-manager/overview.md)
- [ComfyUI Integration](../advanced/comfyui-integration.md)
- [Terminology](../tips/terminology.md)
