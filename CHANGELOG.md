# Changelog

All notable changes to Stability Matrix will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning 2.0](https://semver.org/spec/v2.0.0.html).

## v2.8.0-pre.4
### Added
- Added Recommended Models dialog after one-click installer
### Changed
- Changed one-click installer to match the new package installation style
### Fixed
- Fixed Environment Variables not being editable

## v2.8.0-pre.3
### Added
- Added "Config" Shared Model Folder option for Fooocus
- Added "Copy Details" button to Unexpected Error dialog
### Changed
- (Internal) Updated to Avalonia 11.0.7
- Changed the Close button on the package install dialog to "Hide" 
  - Functionality remains the same, just a name change
- Updated French translation (thanks Greg!)
### Fixed
- Webp static images can now be shown alongside existing webp animation support
- Fixed image gallery arrow key navigation requiring clicking before responding
- Fixed crash when loading extensions list with no internet connection
- Fixed crash when invalid launch arguments are passed
- Fixed "must give at least one requirement to install" error when installing extensions with empty requirements.txt

## v2.8.0-pre.2
### Added
- Added German language option, thanks to Mario da Graca for the translation
- Added Portuguese language options, thanks to nextosai for the translation
### Changed
- Updated translations for the following languages:
  - Spanish
  - French
  - Japanese
  - Turkish
### Fixed
- Fixed Auto-update failing to start new version on Windows and Linux when path contains spaces
- Fixed InvokeAI v3.6.0 `"detail": "Not Found"` error when opening the UI
- Install button will now be properly disabled when the duplicate warning is shown

## v2.8.0-pre.1
### Added
- Added Package Extensions (Plugins) management - accessible from the Packages' 3-dot menu. Currently supports ComfyUI and A1111.
- Added base model filter to Checkpoints page
- Search box on Checkpoints page now searches tags and trigger words
- Added "Compatible Images" category when selecting images for Inference projects
- Added "Find in Model Browser" option to the right-click menu on the Checkpoints page
### Changed
- Removed "Failed to load image" notification when loading some images on the Checkpoints page
- Installed models will no longer be selectable on the Hugging Face tab of the model browser
### Fixed
- Inference file name patterns with directory separator characters will now have the subdirectories created automatically
- Fixed missing up/downgrade buttons on the Python Packages dialog when the version was not semver compatible
- Automatic1111 package installs will now install the missing `jsonmerge` package

## v2.8.0-dev.4
### Added
- Auto-update support for macOS
- New package installation flow
- Added `--use-directml` launch argument for SDWebUI DirectML fork
### Changed
- Changed default Period to "AllTime" in the Model Browser
### Fixed
- Fixed SDTurboScheduler's missing denoise parameter

## v2.8.0-dev.3
### Added
- Added release builds for macOS (Apple Silicon)
- Added new package: [OneTrainer](https://github.com/Nerogar/OneTrainer)
- Added ComfyUI launch argument configs: Cross Attention Method, Force Floating Point Precision, VAE Precision
- Added Delete button to the CivitAI Model Browser details dialog
- Added "Copy Link to Clipboard" for connected models in the Checkpoints page
### Changed
- Python Packages install dialog now allows entering multiple arguments or option flags
### Fixed
- Fixed environment variables grid not being editable related to [Avalonia #13843](https://github.com/AvaloniaUI/Avalonia/issues/13843)

## v2.8.0-dev.2
### Added
#### Inference
- Added Image to Video project type
#### Output Browser
- Added support for webp files
- Added "Send to Image to Image" and "Send to Image to Video" options to the context menu
### Changed
- Changed how settings file is written to disk to reduce potential data loss risk

## v2.8.0-dev.1
### Added
#### Inference
- Added image and model details in model selection boxes
- Added CLIP Skip setting, toggleable from the model settings button

## v2.7.9
### Fixed
- Fixed InvokeAI v3.6.0 `"detail": "Not Found"` error when opening the UI

## v2.7.8
### Changed
- Python Packages install dialog now allows entering multiple arguments or option flags
### Fixed
- Fixed InvokeAI Package dependency versions ([#395](https://github.com/LykosAI/StabilityMatrix/pull/395))

## v2.7.7
### Added
- Added `--use-directml` launch argument for SDWebUI DirectML fork
### Changed
- Model Browser downloads will no longer be disabled if the free drive space is unavailable
- Default Linux installation folder changed to prevent issues with hidden folders
- Changed default Period to "AllTime" in the Model Browser
### Fixed
- Fixed error where Environment Variables were not editable
- Fixed SDTurboScheduler's missing denoise parameter

## v2.7.6
### Added
- Added SDXL Turbo and Stable Video Diffusion to the Hugging Face tab
### Changed
- ControlNet model selector will now show the parent directory of a model when relevant
### Fixed
- Fixed Python Packages dialog crash due to pip commands including warnings
- Fixed Base Model downloads from the Hugging Face tab downloading to the wrong folder
- Fixed InvokeAI `! [rejected] v3.4.0post2 -> v3.4.0post2 (would clobber existing tag)` error on updating to the latest version 
- Fixed settings not saving in some scenarios, such as when the `settings.json` file existed but was empty

## v2.7.5
### Fixed
- Fixed Python Packages manager crash when pip list returns warnings in json
- Fixed slowdown when loading PNGs with large amounts of metadata
- Fixed crash when scanning directories for missing metadata

## v2.7.4
### Changed
- Improved low disk space handling
### Fixed
- Fixed denoise strength in Inference Text to Image
- Fixed PathTooLongException for IPAdapter folders when using ComfyUI in Symlink mode
- Fixed configs and symlinks not being cleaned up when switched to the opposite mode
- Fixed model indexing stopping when encountering paths longer than 1021 bytes in length
- Fixed repeated nested folders being created in `Models/ControlNet` when using ComfyUI in Symlink mode. Existing folders will be repaired to their original structure on launch.

## v2.7.3
### Added
- Added missing IPAdapter and CLIP Vision folder links for ComfyUI
### Fixed
- Fixed UnicodeDecodeError when using extra_model_paths.yaml in ComfyUI on certain locales
- Fixed SDXL CLIP Vision model directory name conflict
- Fixed [#334](https://github.com/LykosAI/StabilityMatrix/issues/334) - Win32Exception if Settings are opened

## v2.7.2
### Changed
- Changed Symlink shared folder link targets for Automatic1111 and ComfyUI. From `ControlNet -> models/controlnet` to `ControlNet -> models/controlnet/ControlNet` and `T2IAdapter -> models/controlnet/T2IAdapter`.
- Changed FreeU defaults to match recommended SD1.5 defaults
- Changed default denoise strength from 1.0 to 0.7
### Fixed
- Fixed ControlNet / T2IAdapter shared folder links for Automatic1111 conflicting with each other
- Fixed URIScheme registration errors on Linux
- Fixed RuinedFooocus missing output folder on startup
- Fixed incorrect Fooocus VRAM launch arguments

## v2.7.1
### Added
- Added Turkish UI language option, thanks to Progesor for the translation
### Fixed
- Fixed Inference Image to Image projects missing denoise strength setting

## v2.7.0
### Added
#### General
- New package: [RuinedFooocus](https://github.com/runew0lf/RuinedFooocus)
- Added an X button to all search fields to instantly clear them (Esc key also works)
- Added System Information section to Settings
#### Inference
- Added Image to Image project type
- Added Modular custom steps
  - Use the plus button to add new steps (Hires Fix, Upscaler, and Save Image are currently available), and the edit button to enable removing or dragging steps to reorder them. This enables multi-pass Hires Fix, mixing different upscalers, and saving intermediate images at any point in the pipeline.
- Added Sampler addons
  - Addons usually affect guidance like ControlNet, T2I, FreeU, and other addons to come. They apply to the individual sampler, so you can mix and match different ControlNets for Base and Hires Fix, or use the current output from a previous sampler as ControlNet guidance image for HighRes passes.
- Added SD Turbo Scheduler
- Added display names for new samplers ("Heun++ 2", "DDPM", "LCM")
- Added Ctrl+Enter as a shortcut for the Generate Image button
#### Accounts Settings Subpage
- Lykos Account sign-up and login - currently for Patreon OAuth connections but GitHub requests caching and settings sync are planned
- Supporters can now connect your Patreon accounts, then head to the Updates page to choose to receive auto-updates from the Dev or Preview channels
- CivitAI Account login with API key - enables downloading models from the Browser page that require CivitAI logins, more integrations like liking and commenting are also planned
#### Updates Settings Subpage
- Toggle auto-update notifications and manually check for updates
- Choose between Stable, Preview, and Dev update channels
#### Inference Settings Subpage
- Moved Inference settings to subpage
- Updated with more localized labels
#### Outputs Page
- Added Refresh button to update gallery from file system changes
#### Checkpoints Page
- Added the ability to drag & drop checkpoints between different folders 
- Added "Copy Trigger Words" option to the three-dots menu on the Checkpoints page (when data is available)
- Added trigger words on checkpoint card and tooltip
- Added "Find Connected Metadata" options for root-level and file-level scans
- Added "Update Existing Metadata" button
#### Model Browser
- Added Hugging Face tab to the Model Browser
- Added additional base model filter options for CivitAI ("SD 1.5 LCM", "SDXL 1.0 LCM", "SDXL Turbo", "Other")
- Added the ability to type in a specific page number in the CivitAI Model Browser
- Right clicking anywhere on the model card will open the same menu as the three-dots button
- New model downloads will save trigger words in metadata, if available
- Model author username and avatar display, with clickable link to their profile
### Changed
#### General
- Model Browser page has been redesigned, featuring more information like rating and download counts
- Model Browser navigation has improved animations on hover and compact number formatting
- Updated Outputs Page button and menu layout
- Rearranged Add Package dialog slightly to accommodate longer package list
- Folder-level "Find Connected Metadata" now scans the selected folder and its subfolders
- Model Browser now split into "CivitAI" and "Hugging Face" tabs
#### Inference
- Selected images (i.e. Image2Image, Upscale, ControlNet) will now save their source paths saved and restored on load. If the image is moved or deleted, the selection will show as missing and can be reselected
- Project files (.smproj) have been updated to v3, existing projects will be upgraded on load and will no longer be compatible with older versions of Stability Matrix
### Fixed
- Fixed Outputs page reverting back to Shared Output Folder every time the page is reloaded
- Potentially fixed updates sometimes clearing settings or launching in the wrong directory
- Improved startup time and window load time after exiting dialogs
- Fixed control character decoding that caused some progress bars to show as `\u2588`
- Fixed Python `rich` package's progress bars not showing in console
- Optimized ProgressRing animation bindings to reduce CPU usage
- Improved safety checks in custom control rendering to reduce potential graphical artifacts
- Improved console rendering safety with cursor line increment clamping, as potential fix for [#111](https://github.com/LykosAI/StabilityMatrix/issues/111)
- Fixed [#290](https://github.com/LykosAI/StabilityMatrix/issues/290) - Model browser crash due to text trimming certain unicode characters
- Fixed crash when loading an empty settings file
- Improve Settings save and load performance with .NET 8 Source Generating Serialization
- Fixed ApplicationException during database shutdown
- InvokeAI model links for T2I/IpAdapters now point to the correct folders
- Added extra checks to help prevent settings resetting in certain scenarios
- Fixed Refiner model enabled state not saving to Inference project files
- Fixed NullReference error labels when clearing the Inference batch size settings, now shows improved message with minimum and maximum value constraints

## v2.7.0-pre.4
### Added
#### Inference
- Added Image to Image project type
- Added Modular custom steps
  - Use the plus button to add new steps (Hires Fix, Upscaler, and Save Image are currently available), and the edit button to enable removing or dragging steps to reorder them. This enables multi-pass Hires Fix, mixing different upscalers, and saving intermediate images at any point in the pipeline.
- Added Sampler addons
  - Addons usually affect guidance like ControlNet, T2I, FreeU, and other addons to come. They apply to the individual sampler, so you can mix and match different ControlNets for Base and Hires Fix, or use the current output from a previous sampler as ControlNet guidance image for HighRes passes.
- Added SD Turbo Scheduler
- Added display names for new samplers ("Heun++ 2", "DDPM", "LCM")
#### Model Browser
- Added additional base model filter options ("SD 1.5 LCM", "SDXL 1.0 LCM", "SDXL Turbo", "Other")
### Changed
#### Inference
- Selected images (i.e. Image2Image, Upscale, ControlNet) will now save their source paths saved and restored on load. If the image is moved or deleted, the selection will show as missing and can be reselected
- Project files (.smproj) have been updated to v3, existing projects will be upgraded on load and will no longer be compatible with older versions of Stability Matrix
### Fixed
- Fixed Refiner model enabled state not saving to Inference project files
 
## v2.7.0-pre.3
### Added
- Added "Find Connected Metadata" options for root-level and file-level scans to the Checkpoints page
- Added "Update Existing Metadata" button to the Checkpoints page
- Added Hugging Face tab to the Model Browser
- Added the ability to type in a specific page number in the CivitAI Model Browser
### Changed
- Folder-level "Find Connected Metadata" now scans the selected folder and its subfolders
- Model Browser now split into "CivitAI" and "Hugging Face" tabs
### Fixed
- InvokeAI model links for T2I/IpAdapters now point to the correct folders
- Added extra checks to help prevent settings resetting in certain scenarios

## v2.7.0-pre.2
### Added
- Added System Information section to Settings
### Changed
- Moved Inference Settings to subpage
### Fixed
- Fixed crash when loading an empty settings file
- Improve Settings save and load performance with .NET 8 Source Generating Serialization
- Fixed ApplicationException during database shutdown

## v2.7.0-pre.1
### Fixed
- Fixed control character decoding that caused some progress bars to show as `\u2588`
- Fixed Python `rich` package's progress bars not showing in console
- Optimized ProgressRing animation bindings to reduce CPU usage
- Improved safety checks in custom control rendering to reduce potential graphical artifacts
- Improved console rendering safety with cursor line increment clamping, as potential fix for [#111](https://github.com/LykosAI/StabilityMatrix/issues/111)

## v2.7.0-dev.4
### Fixed
- Fixed [#290](https://github.com/LykosAI/StabilityMatrix/issues/290) - Model browser crash due to text trimming certain unicode characters 

## v2.7.0-dev.3
### Added
- New package: [RuinedFooocus](https://github.com/runew0lf/RuinedFooocus)
#### Model Browser
- Right clicking anywhere on the model card will open the same menu as the three-dots button
- New model downloads will save trigger words in metadata, if available
- Model author username and avatar display, with clickable link to their profile
#### Checkpoints Page
- Added "Copy Trigger Words" option to the three-dots menu on the Checkpoints page (when data is available)
- Added trigger words on checkpoint card and tooltip
### Changed
#### Model Browser
- Improved number formatting with K/M suffixes for download and favorite counts
- Animated zoom effect on hovering over model images
#### Checkpoints Page
- Rearranged top row layout to use CommandBar
### Fixed
- Improved startup time and window load time after exiting dialogs

## v2.7.0-dev.2
### Added
#### General
- Added an X button to all search fields to instantly clear them (Esc key also works) 
#### Outputs Page 
- Added Refresh button to update gallery from file system changes
#### Checkpoints Page
- Added the ability to drag & drop checkpoints between different folders
### Changed
#### Outputs Page
- Updated button and menu layout
#### Packages Page
- Rearranged Add Package dialog slightly to accommodate longer package list
### Fixed
- Fixed InvalidOperation errors when signing into accounts shortly after signing out, while the previous account update is still running
- Fixed Outputs page reverting back to Shared Output Folder every time the page is reloaded
- Potentially fixed updates sometimes clearing settings or launching in the wrong directory

## v2.7.0-dev.1
### Added
- Accounts Settings Subpage
  - Lykos Account sign-up and login - currently for Patreon OAuth connections but GitHub requests caching and settings sync are planned
  - Supporters can now connect your Patreon accounts, then head to the Updates page to choose to receive auto-updates from the Dev or Preview channels
  - CivitAI Account login with API key - enables downloading models from the Browser page that require CivitAI logins, more integrations like liking and commenting are also planned
- Updates Settings Subpage
  - Toggle auto-update notifications and manually check for updates
  - Choose between Stable, Preview, and Dev update channels
### Changed
- Model Browser page has been redesigned, featuring more information like rating and download counts

## v2.6.7
### Fixed
- Fixed prerequisite install not unpacking due to improperly formatted 7z argument (Caused the "python310._pth FileNotFoundException")
- Fixed [#301](https://github.com/LykosAI/StabilityMatrix/issues/301) - Package updates failing silently because of a PortableGit error

## v2.6.6
### Fixed
- Fixed [#297](https://github.com/LykosAI/StabilityMatrix/issues/297) - Model browser LiteAsyncException occuring when fetching entries with unrecognized values from enum name changes

## v2.6.5
### Fixed
- Fixed error when receiving unknown model format values from the Model Browser
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
