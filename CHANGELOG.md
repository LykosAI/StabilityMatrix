# Changelog

All notable changes to Stability Matrix will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning 2.0](https://semver.org/spec/v2.0.0.html).

## v2.6.5
### Fixed
- Fixed process errors when installing or updating Pip packages using the Python packages dialog

## v2.6.4
### Fixed
- Fixed errors preventing Model Browser from finding results with certain search queries

## v2.6.3
### Fixed
- Fixed InvalidOperationException during prerequisite installs on certain platforms where process name and duration reporting are not supported

## v2.6.2
### Changed
- Backend changes for auto-update schema v3, supporting customizable release channels and faster downloads with zip compression
### Fixed
- Better error reporting including outputs for git subprocess errors during package install / update
- Fixed `'accelerate' is not recognized as an internal or external command` error when starting training in kohya_ss
- Fixed some instances of `ModuleNotFoundError: No module named 'bitsandbytes.cuda_setup.paths'` error when using 8-bit optimizers in kohya_ss
- Fixed errors preventing Inference outputs from loading in the img2img tabs of other packages 

## v2.6.1
### Changed
- NVIDIA GPU users will be updated to use CUDA 12.1 for the InvokeAI package for a slight performance improvement
  - Update will occur the next time the package is updated, or on a fresh install
  - Note: CUDA 12.1 is only available on Maxwell (GTX 900 series) and newer GPUs  
### Fixed
- Reduced the amount of calls to GitHub to help prevent rate limiting
- Fixed rate limit crash on startup preventing app from starting

## v2.6.0
### Added
- Added **Output Sharing** option for all packages in the three-dots menu on the Packages page
  - This will link the package's output folders to the relevant subfolders in the "Outputs" directory
    - When a package only has a generic "outputs" folder, all generated images from that package will be linked to the "Outputs\Text2Img" folder when this option is enabled
- Added **Outputs page** for viewing generated images from any package, or the shared output folder
- Added [Stable Diffusion WebUI/UX](https://github.com/anapnoe/stable-diffusion-webui-ux) package
- Added [Stable Diffusion WebUI-DirectML](https://github.com/lshqqytiger/stable-diffusion-webui-directml) package
- Added [kohya_ss](https://github.com/bmaltais/kohya_ss) package
- Added [Fooocus-ControlNet-SDXL](https://github.com/fenneishi/Fooocus-ControlNet-SDXL) package
- Added GPU compatibility badges to the installers
- Added filtering of "incompatible" packages (ones that do not support your GPU) to all installers 
  - This can be overridden by checking the new "Show All Packages" checkbox
- Added more launch options for Fooocus, such as the `--preset` option
- Added Ctrl+ScrollWheel to change image size in the inference output gallery and new Outputs page
- Added "No Images Found" placeholder for non-connected models on the Checkpoints tab
- Added "Open on GitHub" option to the three-dots menu on the Packages page
### Changed
- If ComfyUI for Inference is chosen during the One-Click Installer, the Inference page will be opened after installation instead of the Launch page
- Changed all package installs & updates to use git commands instead of downloading zip files
- The One-Click Installer now uses the new progress dialog with console
- NVIDIA GPU users will be updated to use CUDA 12.1 for ComfyUI & Fooocus packages for a slight performance improvement
  - Update will occur the next time the package is updated, or on a fresh install
  - Note: CUDA 12.1 is only available on Maxwell (GTX 900 series) and newer GPUs
- Improved Model Browser download stability with automatic retries for download errors
- Optimized page navigation and syntax formatting configurations to improve startup time
### Fixed
- Fixed crash when clicking Inference gallery image after the image is deleted externally in file explorer
- Fixed Inference popup Install button not working on One-Click Installer
- Fixed Inference Prompt Completion window sometimes not showing while typing
- Fixed "Show Model Images" toggle on Checkpoints page sometimes displaying cut-off model images
- Fixed missing httpx package during Automatic1111 install
- Fixed some instances of localized text being cut off from controls being too small

## v2.5.7
### Fixed
- Fixed error `got an unexpected keyword argument 'socket_options'` on fresh installs of Automatic1111 Stable Diffusion WebUI due to missing httpx dependency specification from gradio

## v2.5.6
### Added
- Added Russian UI language option, thanks to aolko for the translation

## v2.5.5
### Added
- Added Spanish UI language options, thanks to Carlos Baena and Lautaroturina for the translations
- Manual input prompt popup on package input requests besides Y/n confirmations
- Added `--disable-gpu` launch argument to disable hardware accelerated rendering
### Fixed
- Fixed infinite progress wheel when package uninstall fails

## v2.5.4
### Fixed
- Fixed [#208](https://github.com/LykosAI/StabilityMatrix/issues/208) - error when installing xformers

## v2.5.3
### Added
- Added French UI language option, thanks to eephyne for the translation
### Fixed
- Fixed Automatic 1111 missing dependencies on startup by no longer enabling `--skip-install` by default.

## v2.5.2
### Added
- Right click Inference Batch options to enable selecting a "Batch Index". This can be used to reproduce a specific image from a batch generation. The field will be automatically populated in metadata of individual images from a batch generation.
  - The index is 1-based, so the first image in a batch is index 1, and the last image is the batch size.
  - Currently this generates different individual images for batches using Ancestral samplers, due to an upstream ComfyUI issue with noise masking. Looking into fixing this.
- Inference Batches option now is implemented, previously the setting had no effect
### Changed
- Default upscale factor for Inference is now 2x instead of 1x
### Fixed
- Fixed batch combined image grids not showing metadata and not being importable
- Fixed "Call from invalid thread" errors that sometimes occured during update notifications

## v2.5.1
### Added
- `--skip-install` default launch argument for Automatic1111 Package
### Fixed
- Fixed Prompt weights showing syntax error in locales where decimal separator is not a period

## v2.5.0
### Added
- Added Inference, a built-in native Stable Diffusion interface, powered by ComfyUI
- Added option to change the Shared Folder method for packages using the three-dots menu on the Packages page
- Added the ability to Favorite models in the Model Browser
- Added "Favorites" sort option to the Model Browser
- Added notification flyout for new available updates. Dismiss to hide until the next update version.
- Added Italian UI language options, thanks to Marco Capelli for the translations
### Changed
- Model Browser page size is now 20 instead of 14  
- Update changelog now only shows the difference between the current version and the latest version
### Fixed
- Fixed [#141](https://github.com/LykosAI/StabilityMatrix/issues/141) - Search not working when sorting by Installed on Model Browser
- Fixed SD.Next not showing "Open Web UI" button when finished loading
- Fixed model index startup errors when `./Models` contains unknown custom folder names
- Fixed ストップ button being cut off in Japanese translation 
- Fixed update progress freezing in some cases
- Fixed light theme being default in first time setup window
- Fixed shared folder links not recreating fully when partially missing

## v2.4.6
### Added
- LDSR / ADetailer shared folder links for Automatic1111 Package
### Changed
- Made Dark Mode background slightly lighter

## v2.4.5
### Fixed
- Fixed "Library Dir not set" error on launch

## v2.4.4
### Added
- Added button to toggle automatic scrolling of console output
### Fixed
- Fixed [#130](https://github.com/LykosAI/StabilityMatrix/issues/130) ComfyUI extra_model_paths.yaml file being overwritten on each launch
- Fixed some package updates not showing any console output
- Fixed auto-close of update dialog when package update is complete 

## v2.4.3
### Added
- Added "--no-download-sd-model" launch argument option for Stable Diffusion Web UI
- Added Chinese (Simplified) and Chinese (Traditional) UI language options, thanks to jimlovewine for the translations
### Changed
- Package updates now use the new progress dialog with console output
### Fixed
- Updated Japanese translation for some terms

## v2.4.2
### Added
- Added Japanese UI language option, thanks to kgmkm_mkgm for the translation
- Language selection available in Settings, and defaults to system language if supported

## v2.4.1
### Fixed
- Fixed deleting checkpoints not updating the visual grid until the page is refreshed
- Fixed updates sometimes freezing on "Installing Requirements" step

## v2.4.0
### Added
- New installable Package - [Fooocus-MRE](https://github.com/MoonRide303/Fooocus-MRE)
- Added toggle to show connected model images in the Checkpoints tab
- Added "Find Connected Metadata" option to the context menu of Checkpoint Folders in the Checkpoints tab to connect models that don't have any metadata
### Changed
- Revamped package installer
  - Added "advanced options" section for commit, shared folder method, and pytorch options
  - Can be run in the background
  - Shows progress in the Downloads tab
- Even more performance improvements for loading and searching the Checkpoints page
### Fixed
- Fixed [#97](https://github.com/LykosAI/StabilityMatrix/issues/97) - Codeformer folder should now get linked correctly
- Fixed [#106](https://github.com/LykosAI/StabilityMatrix/issues/106) - ComfyUI should now install correctly on Windows machines with an AMD GPU using DirectML
- Fixed [#107](https://github.com/LykosAI/StabilityMatrix/issues/107) - Added `--autolaunch` option to SD.Next
- Fixed [#110](https://github.com/LykosAI/StabilityMatrix/issues/110) - Model Browser should properly navigate to the next page of Installed models
- Installed tag on model browser should now show for connected models imported via drag & drop

## v2.3.4
### Fixed
- Fixed [#108](https://github.com/LykosAI/StabilityMatrix/issues/108) - (Linux) Fixed permission error on updates [#103](https://github.com/LykosAI/StabilityMatrix/pull/103)

## v2.3.3
### Fixed
- Fixed GPU recognition for Nvidia Tesla GPUs
- Fixed checkpoint file index extension identification with some path names
- Fixed issue where config file may be overwritten during Automatic1111 package updates
- Fixed "Directory Not Found" error on startup when previously selected Data directory does not exist
- Fixed [#83](https://github.com/LykosAI/StabilityMatrix/issues/83) - Display of packages with long names in the Package Manager
- Fixed [#64](https://github.com/LykosAI/StabilityMatrix/issues/64) - Package install error if venv already exists

## v2.3.2
### Added
- Added warning for exFAT / FAT32 drives when selecting a data directory
### Fixed
- Automatic1111 and ComfyUI should now install the correct version of pytorch for AMD GPUs
- Fixed "Call from invalid thread" exceptions preventing download completion notifications from showing
- Fixed model preview image downloading with incorrect name
### Changed
- Redesigned "Select Model Version" dialog to include model description and all preview images

## v2.3.1
### Fixed
- Fixed Auto update not appearing in some regions due to date formatting issues
- Local package import now migrates venvs and existing models

## v2.3.0
### Added
- New installable Package - [Fooocus](https://github.com/lllyasviel/Fooocus)
- Added "Select New Data Directory" button to Settings
- Added "Skip to First/Last Page" buttons to the Model Browser
- Added VAE as a checkpoint category in the Model Browser
- Pause/Resume/Cancel buttons on downloads popup. Paused downloads persists and may be resumed after restarting the app
- Unknown Package installs in the Package directory will now show up with a button to import them
### Fixed
- Fixed issue where model version wouldn't be selected in the "All Versions" section of the Model Browser
- Improved Checkpoints page indexing performance
- Fixed issue where Checkpoints page may not show all checkpoints after clearing search filter
- Fixed issue where Checkpoints page may show incorrect checkpoints for the given filter after changing pages
- Fixed issue where Open Web UI button would try to load 0.0.0.0 addresses
- Fixed Dictionary error when launch arguments saved with duplicate arguments
- Fixed Launch arguments search not working
### Changed
- Changed update method for SD.Next to use the built-in upgrade functionality
- Model Browser navigation buttons are no longer disabled while changing pages

## v2.2.1
### Fixed
- Fixed SD.Next shared folders config not working with new config format, reverted to Junctions / Symlinks

## v2.2.1

### Fixed
- Fixed SD.Next shared folders config not working with new config format, reverted to Junctions / Symlinks

## v2.2.0

### Added
- Added option to search by Base Model in the Model Browser
- Animated page transitions

### Fixed
- Fixed [#59](https://github.com/LykosAI/StabilityMatrix/issues/61) - `GIT` environment variable is now set for the embedded portable git on Windows as A1111 uses it instead of default `PATH` resolution
- Fixed embedded Python install check on Linux when an incompatible windows DLL is in the Python install directory
- Fixed "ObjectDisposed" database errors that sometimes appeared when closing the app

### Changed
- Revamped Package Manager UI
- InvokeAI installations can now use checkpoints from the Shared Models folder

## v2.1.2

### Changed
- SD.Next install now uses ROCm PyTorch backend on Linux AMD GPU machines for better performance over DirectML

## v2.1.1

### Added
- Discord Rich Presence support can now be enabled in Settings

### Fixed
- Launch Page selected package now persists in settings

## v2.1.0

### Added
- New installable Package - [VoltaML](https://github.com/VoltaML/voltaML-fast-stable-diffusion)
- New installable Package - [InvokeAI](https://github.com/invoke-ai/InvokeAI)
- Launch button can now support alternate commands / modes - currently only for InvokeAI
  > ![](https://github.com/LykosAI/StabilityMatrix/assets/13956642/16a8ffdd-a3cb-4f4f-acc5-c062d3ade363)
- Settings option to set global environment variables for Packages
  > ![](https://github.com/LykosAI/StabilityMatrix/assets/13956642/d577918e-82bb-46d4-9a3a-9b5318d3d4d8)

### Changed
- Compatible packages (ComfyUI, Vlad/SD.Next) now use config files / launch args instead of symbolic links for shared model folder redirect

### Fixed
- Fixed [#48](https://github.com/LykosAI/StabilityMatrix/issues/48) - model folders not showing in UI when they were empty
- Updater now shows correct current version without trailing `.0`
- Fixed program sometimes starting off-screen on multi-monitor setups 
- Fixed console input box transparency
- Fixed [#52](https://github.com/LykosAI/StabilityMatrix/issues/52) - A1111 default approx-vae model download errors by switching default preview method to TAESD
- Fixes [#50](https://github.com/LykosAI/StabilityMatrix/issues/50) - model browser crash when no model versions exist
- Fixed [#31](https://github.com/LykosAI/StabilityMatrix/issues/31) - missing ControlNet link to Shared Models Folder for SD.Next
- Fixed [#49](https://github.com/LykosAI/StabilityMatrix/issues/49) - download progress disappearing when changing pages in Model Browser

## v2.0.4

### Fixed
- Fixed Model Browser downloading files without extensions

## v2.0.3

### Added
- (Windows) New settings option to add Stability Matrix to the start menu
- (Windows) Improved background "Mica" effect on Windows 11, should be smoother with less banding artifacts

### Fixed
- Fixed model categories sometimes not showing if they are empty
- Improved model download hash verification performance
- Fixed some text wrapping visuals on expanded model version dialog on model browser
- Added cancel button for create folder dialog
- One click first time installer now defaults to using the "Package" name instead of the display name ("stable-diffusion-webui" instead of "Stable Diffusion WebUI") for the install folder name - probably safer against upstream issues on folder names with spaces.

## v2.0.2

### Fixed
- (Linux) Updater now sets correct execute permissions
- Image loading (i.e. Checkpoints File preview thumbnail) now has a notification for unsupported local image formats instead of crashing
- Fix unable to start app issues on some machines and dropdowns showing wrong categories - disabled assembly trimming

## v2.0.1

### Added
- Fully rewritten using Avalonia for improved UI and cross-platform support, our biggest update so far, with over 18,000 lines of code.
- Release support for Windows and Linux, with macOS coming soon
- Model Browser now indicates models that are already downloaded / need updates
- Checkpoints Manager now supports filtering/searching
- One-click installer now suggests all 3 WebUI packages for selection
- Hardware compatibility and GPU detection is now more accurate
- Download Indicator on the nav menu for ongoing downloads and progress; supports multiple concurrent model downloads
- Improved console with syntax highlighting, and provisional ANSI rendering for progress bars and advanced graphics
- Input can now be sent to the running package process using the top-right keyboard button on the Launch page. Package input requests for a (y/n) response will now have an interactive popup.

### Fixed
- Fixed crash on exit
- Fixed updating from versions prior to 2.x.x
- Fixed page duplication memory leak that caused increased memory usage when switching between pages
- Package page launch button will now navigate and launch the package, instead of just navigating to launch page
