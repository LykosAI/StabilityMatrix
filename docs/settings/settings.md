# Settings

The Settings page is the central configuration hub for Stability Matrix. It is organized into categories that cover general application behavior, integrations, package management, browsing, appearance, and system-level configuration. These settings can be accessed via the Settings link in the bottom-left of the Stability Matrix UI.

[`Home`](../README.md)

## Table of Contents

- [General](#general)
- [Integrations](#integrations)
- [Checkpoint Manager](#checkpoint-manager)
- [Package Environment](#package-environment)
- [Model Browser](#model-browser)
- [Workflow Browser](#workflow-browser)
- [Console](#console)
- [Appearance](#appearance)
- [System](#system)
- [About](#about)
- [Debug Options](#debug-options)

---

## General

Controls Inference UI behavior, prompt auto-completion, and notification preferences.

### Inference

Inference settings control the behavior of Stability Matrix's built-in image generation and prompt editing tools. They are organized into three groups: **Prompt**, **General**, and **Dimensions**.

#### Prompt

**Auto Completion** provides tag suggestions as you type in prompt and negative prompt text boxes. When enabled, a dropdown appears with matching tags from a configurable CSV file.

| Setting | Description |
|---|---|
| **Enable Auto Completion** | Toggle the tag suggestion feature on or off. |
| **Prompt Tags** | Select which CSV tag file to use for completions. Custom tag CSVs can be imported via the **Import** button. Imported CSVs are copied to the `Tags/` folder inside the data directory. |
| **Replace underscores with spaces** | When enabled, underscores in completed tags are displayed as spaces (e.g., `oil_painting` shows as `oil painting`). |

#### General

| Setting | Description |
|---|---|
| **Filter Extra Networks by Base Model** | When enabled, LoRA and extra network model lists are filtered to only show models compatible with the currently selected base model (e.g., SDXL LoRAs when SDXL is selected). |
| **Image Viewer — Show pixel grid at high zoom** | When enabled, a pixel grid overlay appears when zooming into generated images. |
| **Image Viewer — Move files to Recycle Bin** | When enabled, deleted images go to the system Recycle Bin / Trash instead of being permanently erased. Only shown on platforms where Recycle Bin is available. |
| **Output Image Files — File name pattern** | A format string for naming saved output images, using variables like `{model_name}` and `{seed}`. Clicking the text field shows all available variables. Default: `{model_name}_{seed}_{width}x{height}`. |
| **Floating Search** | When enabled, all combo boxes use floating search mode where typed text appears as an overlay and auto-selects the first match. |

#### Dimensions

| Setting | Description |
|---|---|
| **Step Size** | How much width/height values change per step when using arrow buttons or scroll wheel on dimension inputs. Range: 8–1024 pixels. |
| **Favorite Dimensions** | A configurable list of frequently used width × height pairs that appear as quick-select options in the Inference UI. Add via the **+ Add** button, remove by selecting a row and clicking **Remove**. Saved automatically. |

### Notifications

Controls how Stability Matrix alerts you about events. Each notification type can be independently set to one of three channels:

| Channel | Description |
|---|---|
| **None** | No notification is sent. |
| **In-App** | A toast notification appears inside the Stability Matrix window. |
| **Desktop** | A native OS push notification (Windows Action Center, macOS Notification Center). |

> [!NOTE]
> On Linux, the default channel is **In-App** for all events since native push support varies across desktop environments.

| Event | Default |
|---|---|
| Inference Prompt Completed | Desktop (In-App on Linux) |
| Inference Batch Completed | Desktop (In-App on Linux) |
| Download Completed | Desktop (In-App on Linux) |
| Download Failed | Desktop (In-App on Linux) |
| Download Canceled | Desktop (In-App on Linux) |
| Package Install Completed | Desktop (In-App on Linux) |
| Package Install Failed | Desktop (In-App on Linux) |

---

## Integrations

The Integrations section connects Stability Matrix to external services and platforms.

### Accounts

The Accounts sub-page manages external service credentials. Connecting accounts allows Stability Matrix to download models that require authentication, access membership features, and use cloud-based image generation services.

#### Membership and Lykos Account

A Lykos account is the central identity for Stability Matrix. Signing in unlocks membership benefits and account-linked features. Membership is managed at [lykos.ai/membership](https://lykos.ai/membership?).

| Tier | Description |
|---|---|
| **Visionary** | Highest monthly credits and vote power. Shout-out in both release and Preview/Dev changelog. Maximum support for development. |
| **Pioneer** | Higher monthly credits and vote power. Shout-out in release changelog. |
| **Insider** | Increased credits and vote power over Supporter. Accelerated Model Discovery |
| **Supporter** | 1,000 monthly credits, 2× vote power. The entry-level tier that supports development and server costs. |

All support tiers receive early access to new features through **Preview** and **Dev** update channels.

When you are an active subscriber, the Accounts page shows your tier, the date you started supporting, and a quick link to manage your membership. A thank-you message and community Discord link are also displayed.

If you are not subscribed, a membership card appears with a **Become a Supporter** button that opens the Lykos membership page in your browser.

**Connecting**: Click **Connect** under the Lykos section. A device authentication dialog appears with a code. Open the URL in your browser, enter the code, and authorize. Authentication happens entirely in your browser — no credentials are entered into Stability Matrix.

Your profile image is fetched from [Gravatar](https://gravatar.com/) using your Lykos account email. Click the image to manage your Lykos account, edit your Gravatar, or copy your user ID.

#### CivitAI API Key

Enables downloading early-access models from CivitAI and browsing NSFW-tagged models (requires enabling the NSFW toggle in the Model Browser).

**To connect**: Log in to [civitai.com](https://civitai.com), go to **Account Settings → API Keys**, create a key, and paste it into the Connect dialog in Stability Matrix. The key is validated by fetching your CivitAI profile.

The API key is sent as a `Bearer` token in the `Authorization` header for all requests to `civitai.com`, including model downloads.

#### HuggingFace Token

Enables downloading gated models (e.g., FLUX) from the Hugging Face Hub. You must also accept the model's license terms on the Hugging Face website before downloading.

**To connect**: Log in to [huggingface.co](https://huggingface.co), go to **Settings → Access Tokens**, create a **Read** token (starts with `hf_`), and paste it into the Connect dialog. Stability Matrix validates the token by calling the HuggingFace API.

The token is used in two ways: sent as a `Bearer` header for direct downloads from `huggingface.co`, and available for manual injection into package environments via `HF_TOKEN` in the Environment Variables editor.

> [!IMPORTANT]
> Disconnecting the token here does **not** remove a manually-set `HF_TOKEN` from the Environment Variables editor. Remove it from both places if needed.

#### Gemini API Key

Enables the **Nano Banana** image generation provider in the **Image Lab**, which uses Google's Gemini models. Requires a paid-tier Google AI account with billing enabled.

**To connect**: Go to [Google AI Studio](https://aistudio.google.com/api-keys), create an API key, and paste it into the Connect dialog under the **Image Generation APIs** section. The key is validated at generation time when the Image Lab makes its first request.

> [!IMPORTANT]
> When API keys are generated on their respective service website, it is recommended to save your API keys somewhere safe (ie: text file, password/note manager, etc.) as these services will typically only provide this key once.

#### How Credentials Are Stored

All API keys and tokens are stored encrypted at rest in the `user-secrets.data` file. Keys are never written to plaintext. The file is encrypted using a key derived from system-specific identifiers, meaning it can only be decrypted on the same machine that wrote it.

The `user-secrets.data` location depends on your operating system:

| Platform | Path |
|---|---|
| Windows | `%APPDATA%\StabilityMatrix\user-secrets.data` (`C:\Users\{username}\AppData\Roaming\StabilityMatrix`) |
| Linux | `~/.config/StabilityMatrix/user-secrets.data` |
| macOS | `~/Library/Application Support/StabilityMatrix/user-secrets.data` |

This is the fixed application data directory — distinct from the user-configurable library / data directory where packages and models are stored.

### Discord Rich Presence

When enabled, your Discord status shows that you are using Stability Matrix.

---

## Checkpoint Manager

Controls how model files are handled when imported and how shared model folders are managed.

| Setting | Description |
|---|---|
| **Import Behaviour** | Choose whether to **Copy** or **Move** files when dragging and dropping them into the Checkpoint Manager. Copy leaves the original in place; Move relocates it. |
| **Remove Symlinks on Shutdown** | When enabled, Stability Matrix removes shared model symlinks and junction points when the application closes. |
| **Reset Checkpoints Cache** | Clears the cached checkpoint index. Use this if models are not appearing correctly or if the index becomes out of date. |

---

## Package Environment

Manages environment variables, Python tooling, and Git configuration used when launching packages.

### Environment Variables

Opens the global environment variable editor. Variables set here are injected into every launched package's process environment. This is useful for configuring PyTorch behavior, setting cache locations, or adding API tokens without editing scripts.

Variables are defined as name/value pairs. Click **Edit** to open the editor, then click **+** to add a new row. Changes apply to future package launches — restart any running packages for new variables to take effect.

Common uses include `PYTORCH_ALLOC_CONF` for GPU memory tuning, `HF_TOKEN` for HuggingFace authentication in packages, `CUDA_VISIBLE_DEVICES` for GPU isolation, and `PATH` modifications. See the [Environment Variables guide](../advanced/environment-variables.md) for a full reference.

> [!NOTE]
> User-set environment variables take priority over any defaults Stability Matrix applies automatically at package launch.

### Embedded Python

Stability Matrix bundles its own Python installation (Python 3.10) stored in the `Assets/Python310/` folder inside the data directory. This embedded Python is separate from the various standalone Python versions used by package virtual environments and is used exclusively for Stability Matrix's own internal operations.

Internally, the embedded Python powers:

- **Virtual environment bootstrapping** — When setting up a package for the first time, the embedded Python is used to install `pip` and `virtualenv` into itself via Python.NET. These tools are then invoked to create the package's own isolated virtual environment. This same bootstrap process runs during one-click installs, data directory migration, and standard package installs.
- **Python version inspection** — The **Check Version** button in Settings uses Python.NET to initialize the embedded Python runtime in-process and retrieve detailed version information.

The embedded Python section in Settings displays the current version and provides actions to run an arbitrary Python command utilizing the embedded Python, clear the Pip cache, or clear the uv cache.

### Git

Runs Git commands against the bundled Git installation. On Windows, also provides an option to enable long path support in Git.

### Show Unsupported Python Versions

When enabled, Python versions outside the supported range for a package are still shown as installation options. Typically left disabled unless troubleshooting.

---

## Model Browser

Configures the behavior of the CivitAI model browser.

| Setting | Description |
|---|---|
| **Auto Search on Load** | When enabled, the Model Browser automatically runs a search with your last-used sort, period, and model type filters when the page opens. Results are sorted by Highest Rated by default. When disabled, you must manually trigger a search. Previous search parameters are restored from your last session regardless of this setting. |
| **Base Model Filter** | Toggle which base model types (SD1.5, SDXL, Pony, Illustrious, Anima, etc.) are shown or hidden in the `Base Model` filter for the Model Browser. |

---

## Workflow Browser

| Setting | Description |
|---|---|
| **Infinite Scrolling** | When enabled, the Workflow Browser continuously loads more results as you scroll. When disabled, results are paginated. |

---

## Console

| Setting | Description |
|---|---|
| **History Size** | Maximum number of log entries retained in the Console view. Set to `-1` for unlimited or `0` to disable history. (Default: 9001) |

---

## Appearance

| Setting | Description |
|---|---|
| **Theme** | Switch between Light, Dark, and System themes. Changes take effect immediately. |
| **Language** | Select the UI language from available community-contributed translations. |
| **Number Format** | Choose how numbers are formatted. Options include default system format or forced period/comma decimal separators. A live preview is shown in the dropdown. |
| **Holiday Mode** | Toggles a festive santa hat overlay on Model Browser card images. **Automatic** shows hats during December, **Enabled** forces them year-round, **Disabled** never shows them. |
| **Always Show Scrollbars** | When enabled, scrollbars are always visible. When disabled, they appear only while scrolling. |

---

## System

System-level settings for updates, data management, hardware preferences, and diagnostic information.

### Updates

| Setting | Description |
|---|---|
| **Update Channel** | Choose between **Stable** (tested releases), **Preview** (newer features, minor issues possible), and **Dev** (latest development builds). Preview and Dev channels require an active Lykos membership. |
| **Auto-Update Frequency** | How often Stability Matrix checks for updates: on startup, daily, or never (manual only). A notification appears when an update is available; updates are never installed without confirmation. |

### Analytics

When enabled, Stability Matrix sends anonymous usage data to Lykos to help prioritize development. The data collected is minimal:

- **Package installs** — package name, version, and whether the install succeeded or failed
- **First-time setup** — which package was selected, which recommended models were chosen, and whether setup was skipped

No hardware information, model browsing history, personal data, or API keys are included. Additionally, each app launch sends the Stability Matrix version, OS description, and runtime identifier to the analytics API at most once per day.

This setting is also presented during first-run setup. Analytics are enabled by default (opt-out); you can change your choice here at any time.

### Add to Start Menu

(Windows only) Adds Stability Matrix to the Start Menu for the current user or all users.

### Maximum Simultaneous Downloads

Limits the number of concurrent model downloads. Does not apply to package installs. Set to `0` for unlimited.

### Select New Data Directory

Change the library location where Stability Matrix stores packages, models, images, and settings. Displays the current path. Changing the path does not move existing data — you must relocate files manually. See [Data Directory](../getting-started/data-directory.md#changing-the-data-directory-later) for further details.

### Select New Models Folder

Change the shared models directory independently from the main data directory. Useful for keeping models on a separate drive.

### App Folders

Quick-access buttons to open key Stability Matrix folders in your file manager: Application Data directory, Logs, Data directory, Checkpoints (Models) shared directory, and Packages directory.

### System Settings

| Setting | Description |
|---|---|
| **Default GPU** | Select which GPU Stability Matrix should prefer for package installs. This influences which PyTorch backend index is automatically selected. |
| **Enable Long Paths** | (Windows only) Enables Windows long path support in the registry to avoid path-length errors with deeply nested package or model folders. |

### System Information

Read-only diagnostic information about your hardware:

- **CPU** — processor model
- **Memory** — total installed and available RAM
- **GPU** — detected graphics cards with driver version and VRAM

---

## About

The About section appears at the bottom of the Settings view. It shows:

- The Stability Matrix app icon and name
- The current version number (e.g., `Version 2.16.1`)
- A **License and Open Source Notices** button that opens a dialog listing third-party licenses for bundled open-source components

Tapping the version number repeatedly (7 times) enables hidden debug options in the Settings UI.

## Debug Options

When debug mode is enabled, a **Debug Options** section appears in the Settings view. It contains diagnostic tools and utilities intended for development and troubleshooting:

### Diagnostics

| Item | Description |
|---|---|
| **Paths** | Displays the current working directory, app directory, base directory, and AppData path. |
| **Compat Info** | Shows platform, portable mode status, library directory state, and other environment details. |
| **GPU Info** | Lists all detected GPUs with their properties. |

### Utilities

| Item | Description |
|---|---|
| **Animation Scale** | Adjusts UI animation speed from 0× (instant) to 2×. Lower values make the UI feel snappier during testing. |
| **Notification** | Fires a test in-app notification to verify the notification system. |
| **Content Dialog** | Opens a test content dialog to verify dialog rendering. |
| **Exceptions** | Buttons to trigger unhandled and dispatcher exceptions for testing error handling and crash reporting. |
| **Download Manager tests** | Adds a test tracked download entry. |
| **Refresh Models Index** | Manually refreshes the local model index used by the Checkpoint Manager. |
| **Make image grid** | Opens a file picker to select images and generates a combined grid image. |
| **Image metadata parser** | Chooses an image and parses its metadata for debugging. |
| **Gemini error dialogs** | Previews the various Image Lab / Nano Banana error states (key not configured, invalid key, quota exceeded, access forbidden, generic failure) without triggering a real API call. |

Additional debug commands may appear dynamically at the bottom of the section.
