# General Settings

General settings is the first section in the Settings view. It provides quick navigation to the two primary configuration areas: **Inference** and **Notifications**.

[`Section Overview`](overview.md) | [`Home`](../README.md)

## Table of Contents

- [Inference](#inference)
- [Notifications](#notifications)

---

## Inference

Inference settings control the behavior of Stability Matrix's built-in image generation and prompt editing tools. These settings are organized into three subsections: **Prompt**, **General**, and **Dimensions**.

### Prompt

#### Auto Completion

The auto completion feature provides tag suggestions as you type in prompt and negative prompt text boxes. When enabled, a dropdown appears with matching tags from a configurable CSV file.

| Setting | Description |
|---|---|
| **Enable Auto Completion** | Toggle the tag suggestion feature on or off. |
| **Prompt Tags** | Select which CSV tag file to use for completions. Custom tag CSVs can be imported via the **Import** button below this setting. |
| **Replace underscores with spaces** | When enabled, underscores in completed tags are displayed as spaces (e.g., `oil_painting` shows as `oil painting`). |

> [!NOTE]
> Imported CSVs are copied to the `Tags/` folder inside the data directory. After importing, the new file appears in the **Prompt Tags** dropdown for selection.

---

### General

#### Filter Extra Networks by Base Model

When enabled, LoRA and other extra network model lists in the Inference UI are filtered to only show models compatible with the currently selected base model (e.g., SDXL LoRAs when SDXL is selected). This reduces clutter and helps avoid loading incompatible models.

#### Image Viewer

| Setting | Description |
|---|---|
| **Show pixel grid at high zoom levels** | When enabled, a pixel grid overlay appears when zooming into generated images, making it easier to inspect fine details. |
| **Move files to the Recycle Bin when deleting** | When enabled, deleted images are sent to the system Recycle Bin / Trash instead of being permanently erased. This option is only visible on platforms where the Recycle Bin is available. |

#### Output Image Files

Controls how generated images are named when saved.

**File name pattern** — A format string that defines the naming convention for saved output images. Uses curly-brace variables like `{model_name}` and `{seed}`. Clicking into the text field shows a tooltip with all available format variables and examples. The default pattern is the same as the Inference UI template:

```
{model_name}_{seed}_{width}x{height}
```

A live preview of the resulting filename is shown below the input.

#### Floating Search

When enabled, all combo boxes in the Inference UI use a floating search mode: typed text appears as an overlay and auto-selects the first match. When disabled, each combo box uses its own configured search behavior.

---

### Dimensions

#### Step Size

Controls how much the width and height values increase or decrease per step when using the arrow buttons or scroll wheel on dimension input fields. The value is in pixels and can range from 8 to 1024.

#### Favorite Dimensions

A configurable list of frequently used width × height pairs. These appear as quick-select options in the Inference UI's dimensions section, letting you switch between common resolutions without typing.

| Action | How |
|---|---|
| **Add** | Click **+ Add**, enter a width and height, then save. |
| **Remove** | Select a row in the grid and click **Remove**. |

The grid supports sorting by dimension value. Favorite dimensions are saved automatically and persist across restarts.

---

## Notifications

The Notifications page controls how Stability Matrix alerts you about events such as completed downloads, finished inference prompts, and package installations.

Each notification type can be independently configured to use one of three channels:

| Channel | Description |
|---|---|
| **None** | No notification is sent for this event. |
| **In-App** | A toast notification appears inside the Stability Matrix window. |
| **Desktop** | A native OS push notification is sent (e.g., Windows Action Center, macOS Notification Center). |

> [!NOTE]
> On Linux, native push notifications depend on the desktop environment's notification daemon. The default for Linux is **In-App** since native push support varies across distributions.

### Available Notification Events

| Event | Description | Default Channel |
|---|---|---|
| **Inference Prompt Completed** | A single prompt has finished generating. | Native Push (In-App on Linux) |
| **Inference Batch Completed** | An entire batch of prompts has finished. | Native Push (In-App on Linux) |
| **Download Completed** | A model or file download has finished successfully. | Native Push (In-App on Linux) |
| **Download Failed** | A download has failed due to an error. | Native Push (In-App on Linux) |
| **Download Canceled** | A download was manually canceled by the user. | Native Push (In-App on Linux) |
| **Package Install Completed** | A package install has finished successfully. | Native Push (In-App on Linux) |
| **Package Install Failed** | A package install has failed. | Native Push (In-App on Linux) |

Each event row shows the event name and a dropdown to select the notification channel. Changes take effect immediately and are saved automatically.
