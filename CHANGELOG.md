# Changelog

All notable changes to Stability Matrix will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning 2.0](https://semver.org/spec/v2.0.0.html).

<<<<<<< HEAD
=======
## v2.13.0-dev.3
### Added
- Added more base model types to the CivitAI Model Browser & Checkpoint Manager

## v2.13.0-dev.2
### Added
- Added new package - [SimpleSDXL](https://github.com/metercai/SimpleSDXL) - many thanks to @NullDev for the contribution!
- Added new package - [FluxGym](https://github.com/cocktailpeanut/fluxgym) - many thanks to @NullDev for the contribution!
- Added a new "Extension Packs" section to the extension manager, allowing you to create packs for easier installation of multiple extensions at once
- Added "Search by Creator" command to Civitai browser context menu
- Added Beta scheduler to the scheduler selector in Inference
- Added zipping of log files and "Show Log in Explorer" button on exceptions dialog for easier support
- Added max concurrent downloads option & download queueing for most downloads
### Changed
- (Internal) Updated to Avalonia 11.1.4
- Adjusted the Branch/Release toggle during package install flow to be a little more obvious
- Updated the Dock library used for Inference - fixes some weirdness with resizing / rearranging panels
### Fixed
- Fixed ComfyUI NF4 extension not installing properly when prompted in Inference
- Fixed [#932](https://github.com/LykosAI/StabilityMatrix/issues/932), [#935](https://github.com/LykosAI/StabilityMatrix/issues/935), [#939](https://github.com/LykosAI/StabilityMatrix/issues/939) - InvokeAI failing to update
- Fixed repeated nested folders being created in `Models/StableDiffusion` when using Forge in Symlink mode in certain conditions. Existing folders will be repaired to their original structure on launch.
- Fixed minimize button not working on macOS
- Fixed InvokeAI model sharing spamming the console with "This may take awhile" in certain conditions
- Fixed text alignment issues in the Downloads tab for certain long names / progress infos
### Supporters
#### Visionaries
- A big thank you to our amazing Visionary-tier Patreon supporter, **Waterclouds**! Your continued support is invaluable!

## v2.13.0-dev.1
### Added
- Added the ability to change the Models directory separately from the rest of the Data directory. This can be set in `Settings > Select new Models Folder`
- Added "Copy" menu to the Inference gallery context menu, allowing you to copy the image or the seed (other params coming soon™️)
- Added InvokeAI model sharing option
### Supporters
#### Visionaries
- A heartfelt thank you to our incredible Visionary-tier Patreon supporter, **Waterclouds**! Your ongoing support means a lot to us, and we’re grateful to have you with us on this journey!

>>>>>>> 6eccc3cd (Merge pull request #873 from ionite34/more-base-models)
## v2.12.3
### Added
- Added new package - [SimpleSDXL](https://github.com/metercai/SimpleSDXL) - many thanks to @NullDev for the contribution!
- Added new package - [FluxGym](https://github.com/cocktailpeanut/fluxgym) - many thanks to @NullDev for the contribution!
- Added more base model types to the CivitAI Model Browser & Checkpoint Manager
### Fixed
- Fixed some cases of FileTransferExists error when running re/Forge or Automatic1111
- Fixed update check not happening on startup for some users
- Fixed error when installing Automatic1111 on macOS

## v2.12.2
### Added
- Added Beta scheduler to the scheduler selector in Inference
### Changed
- (Internal) Updated to Avalonia 11.1.4
### Fixed
- Fixed ComfyUI NF4 extension not installing properly when prompted in Inference
- Fixed [#932](https://github.com/LykosAI/StabilityMatrix/issues/932), [#935](https://github.com/LykosAI/StabilityMatrix/issues/935), [#939](https://github.com/LykosAI/StabilityMatrix/issues/939) - InvokeAI failing to update
- Fixed repeated nested folders being created in `Models/StableDiffusion` when using Forge in Symlink mode in certain conditions. Existing folders will be repaired to their original structure on launch.
- Fixed minimize button not working on macOS
### Supporters
#### Visionaries
- We extend our heartfelt appreciation to our dedicated Visionary-tier Patreon supporter, **Waterclouds**. Your ongoing support is invaluable!
#### Pioneers
- We’d also like to thank our great Pioneer-tier patrons: **tankfox**, **tanangular**, **Mr. Unknown**, and **Szir777**. Your continuous support means a lot!

## v2.12.1
### Fixed
- Fixed [#916](https://github.com/LykosAI/StabilityMatrix/issues/916) - InvokeAI failing to install/update on macOS
- Fixed [#914](https://github.com/LykosAI/StabilityMatrix/issues/914) - Unable to use escaped colon `:` character in Inference prompts
- Fixed [#908](https://github.com/LykosAI/StabilityMatrix/issues/908) - Forge unable to use models from "unet" shared folder
- Fixed [#902](https://github.com/LykosAI/StabilityMatrix/issues/902) - Images from shared outputs folder not displaying properly in Stable Diffusion WebUI-UX
- Fixed [#898](https://github.com/LykosAI/StabilityMatrix/issues/898) - Incorrect launch options for RuinedFooocus
- Fixed index url parsing in Python Packages window causing some packages to not have versions available
- Fixed a crash when switching between Model Sharing options for certain packages
### Supporters
#### Visionaries
- A sincere thank you to our valued Visionary-tier Patreon supporter, **Waterclouds**. Your continued support is truly appreciated, and we’re grateful to have you with us on this journey.
#### Pioneers
- We’d also like to extend our gratitude to our Pioneer-tier patrons: **tankfox**, **tanangular**, **Mr. Unknown**, and **Szir777**. Your ongoing support means a great deal to us!

## v2.12.0
### Added
#### New Packages
- [Fooocus - mashb1t's 1-Up Edition](https://github.com/mashb1t/Fooocus) by mashb1t
- [Stable Diffusion WebUI reForge](https://github.com/Panchovix/stable-diffusion-webui-reForge/) by Panchovix
#### Inference
- Added type-to-search for the Inference model selectors. Start typing while the dropdown is open to navigate the list.
- Added "Model Loader" option to Inference, for loading UNet/GGUF/NF4 models (e.g. Flux)
- Added support for the FP8 version of Flux in the default Model Loader via the "Use Flux Guidance" Sampler Addon
- Added trigger words to the Inference Extra Networks (Lora/Lyco) selector for quick copy & paste
- Image viewer context menus now have 2 options: `Copy (Ctrl+C)` which now always copies the image as a file, and `Copy as Bitmap (Shift+Ctrl+C)` (Available on Windows) which copies to the clipboard as native bitmap. This changes the previous single `Copy` button behavior that would first attempt a native bitmap copy on Windows when available, and fall back to a file copy if not.
- Added Face Detailer module to Inference
#### Package Manager
- Added Python dependencies override table to package installation options, where the default pip packages may be overriden for a package's install and updates. This can be changed later or added to existing packages through `Package Menu > Python Dependencies Override`
- Added "Change Version" option to the package card overflow menu, allowing you to downgrade or upgrade a package to a specific version or commit ([#701](https://github.com/LykosAI/StabilityMatrix/issues/701), [#857](https://github.com/LykosAI/StabilityMatrix/issues/857))
- Added "Disable Update Check" option to the package card overflow menu, allowing you to disable update checks for a specific package
- Added Custom commit option in the Advanced Options for package installs ([#670](https://github.com/LykosAI/StabilityMatrix/issues/670), [#839](https://github.com/LykosAI/StabilityMatrix/issues/839), [#842](https://github.com/LykosAI/StabilityMatrix/issues/842))
- Added macOS support for Fooocus & related forks
- Added Intel OneAPI XPU backend (IPEX) option for SD.Next
#### Checkpoint Manager
- Added new Metadata Editor (accessible via the right-click menu), allowing you to create or edit metadata for models
- Added "New Directory" and "Delete" options to the context menu of the tree view.
- Added new toggle for drag & drop - when enabled, all selected models will now move together with the dragged model
- Added "File Size" sorting option
- Added "Hide Empty Categories" toggle
- Added "Select All" button to the InfoBar (shown when at least one model is selected)
- Added "Show NSFW Images" toggle
#### Model Browser
- Added "Hide Installed Models" toggle to the CivitAI Model Browser
- Added toggle to hide "Early Access" models in the CivitAI Model Browser
- Added ultralytics models to HuggingFace model browser
#### Other
- Added "Sign in with Google" option for connecting your Lykos Account on the Account Settings page
- Added zoom sliders for Outputs, Checkpoints, and Model Browser pages
- Added Settings option "Console: History Size" to adjust the number of lines stored in the console history when running packages. Defaults to 9001 lines.
- Added optional anonymous usage reporting for gauging popularity of package installs and features. You will be asked whether you want to enable this feature on launch, and can change your choice at any time in `Settings > System > Analytics`
- Added "Run Command" option in Settings for running a command with the embedded Python or Git executables
- Added "Enable Long Paths" option for Git to the Settings page
- Added "System Settings > Enable Long Paths" option to enable NTFS long paths on Windows
- Added Korean translations thanks to maakcode!
- (Windows, Linux) Added Vulkan rendering support using launch argument `--vulkan`. (On Windows, the default WinUI composition renderer is likely still preferrable. Linux users are encouraged to try the new renderer to see if it improves performance and responsiveness.)
### Changed
- Optimized image loading across the app, with loading speed now up to 4x faster for local images, and up to 17x faster for remote images
- Image loading in the Outputs page now uses native memory management for ~2x less peak memory usage, and will release memory more quickly when switching away from the Outputs page or scrolling images out of view
- Improved animation fluidity of image rendering while scrolling quickly across large collections (e.g. Outputs, Model Browser)
- ComfyUI will no longer be pinned to torch 2.1.2 for nvidia users on Windows ([#861](https://github.com/LykosAI/StabilityMatrix/issues/861))
- Model browser download progress no longer covers the entire card for the entire duration of the download
- Updated torch index to `rocm6.1` for AMD users of ComfyUI
- Show better error message for early access model downloads
- Updated torch version for a1111 on mac
- Checkpoints tab now shows "image hidden" for images that are hidden by the NSFW filter
- OAuth-type connection errors in Account Settings now show a more detailed error message
- The "Download Failed" message for model downloads is now persistent until dismissed
- Separated the Generate button from the prompt control in Inference so it can be moved like other controls
- Updated translations for Turkish and Russian
- (Internal) Updated Avalonia to 11.1.3 - Includes major rendering and performance optimizations, animation refinements, improved IME / text selection, and improvements for window sizing / z-order / multi-monitor DPI scaling. ([avaloniaui.net/blog/avalonia-11-1-a-quantum-leap-in-cross-platform-ui-development](https://avaloniaui.net/blog/avalonia-11-1-a-quantum-leap-in-cross-platform-ui-development))
- (Internal) Updated SkiaSharp (Rendering Backend) to 3.0.0-preview.4.1, potentially fixes issues with window rendering artifacts on some machines.
- (Internal) Updated other dependencies for security and bug fixes.
### Fixed
- Fixed [#888](https://github.com/LykosAI/StabilityMatrix/issues/888) - error updating kohya_ss due to long paths
- Fixed some ScrollViewers changing scroll position when focus changes
- Fixed CivitAI Model Browser sometimes incorrectly showing "No models found" before toggling "Show NSFW" or "Hide Installed" filters
- Fixed SwarmUI settings being overwritten on launch
- Fixed issue where some Inference-generated images would be saved with the bottom missing
- Fixed [#851](https://github.com/LykosAI/StabilityMatrix/issues/851) - missing fbgemm.dll errors when using latest torch with certain packages
- Fixed issue where ApproxVAE models would show up in the VAE folder
- Fixed [#878](https://github.com/LykosAI/StabilityMatrix/issues/878) - Checkpoints tab will no longer try to browse directories it can't access
- Fixed crash when opening Settings page when refreshing CivitAI account status results in an error
- Fixed [#814](https://github.com/LykosAI/StabilityMatrix/issues/814), [#875](https://github.com/LykosAI/StabilityMatrix/issues/875) - Error when installing RuinedFooocus
- LORAs are now sorted by model name properly in the Extra Networks dropdown
- (macOS) Fixed OAuth connection prompts in Account Settings not automatically updating status after connection. Custom URL schemes are now also supported on macOS builds.
### Supporters
#### Visionaries
- A heartfelt thank you to our Visionary-tier patron, **Waterclouds**! We greatly appreciate your continued support!
#### Pioneers
- A special shoutout to our Pioneer-tier patrons: **tankfox**, **tanangular**, **Mr. Unknown**, and **Szir777**! Your unwavering support means a great deal!

## v2.12.0-pre.3
### Added
- Added Python dependencies override table to package installation options, where the default pip packages may be overriden for a package's install and updates. This can be changed later or added to existing packages through `Package Menu > Python Dependencies Override`
- Added optional anonymous usage reporting for gauging popularity of package installs and features. You will be asked whether you want to enable this feature on launch, and can change your choice at any time in `Settings > System > Analytics`
- Added Korean translations thanks to maakcode!
### Changed
- Show better error message for early access model downloads
- Updated torch version for a1111 on mac
- Checkpoints tab now shows "image hidden" for images that are hidden by the NSFW filter
- Updated translations for Turkish and Russian
### Fixed
- Fixed issue where some Inference-generated images would be saved with the bottom missing
- Fixed CivitAI Browser page scroll refresh not ordering models correctly
- Fixed missing fbgemm.dll errors when using latest torch with certain packages
- Fixed issue where ApproxVAE models would show up in the VAE folder
- Fixed [#878](https://github.com/LykosAI/StabilityMatrix/issues/878) - Checkpoints tab will no longer try to browse directories it can't access
- Fixed crash when opening Settings page when refreshing CivitAI account status results in an error
### Supporters
#### Visionaries
- A huge thank you to our Visionary-tier Patreon supporter, **Waterclouds**! We appreciate your continued support, and are grateful to have you on this journey with us!

## v2.12.0-pre.2
### Added
- Added "Show NSFW Images" toggle to the Checkpoints page
- Added "Model Loader" option to Inference, for loading UNet/GGUF/NF4 models (e.g. Flux)
- Added type-to-search for the Inference model selectors. Start typing while the dropdown is open to navigate the list.
- Added "Sign in with Google" option for connecting your Lykos Account on the Account Settings page
### Changed
- Updated Brazilian Portuguese translations thanks to thiagojramos
- Merged the "Flux Text to Image" workflow back into the main Text to Image workflow
### Fixed
- Fixed CivitAI Model Browser sometimes incorrectly showing "No models found" before toggling "Show NSFW" or "Hide Installed" filters
- Fixed Automatic1111 & related packages not including the gradio-allowed-path argument for the shared output folder
- Fixed SwarmUI settings being overwritten on launch
- Fixed Forge output folder links pointing to the incorrect folder
- LORAs are now sorted by model name properly in the Extra Networks dropdown
- Fixed errors when downloading models with invalid characters in the file name

## v2.12.0-pre.1
### Added
- Added "Hide Installed Models" toggle to the CivitAI Model Browser
### Changed
- ComfyUI will no longer be pinned to torch 2.1.2 for nvidia users on Windows
- Model browser download progress no longer covers the entire card for the entire duration of the download
- Updated torch index to `rocm6.0` for AMD users of ComfyUI
- (Internal) Updated to Avalonia 11.1.2
- OAuth-type connection errors in Account Settings now show a more detailed error message
### Fixed
- Fixed Inference not connecting with "Could not connect to backend - JSON value could not be converted" error with API changes from newer ComfyUI versions
- (macOS) Fixed OAuth connection prompts in Account Settings not automatically updating status after connection. Custom URL schemes are now also supported on macOS builds.

## v2.12.0-dev.3
### Added
- Added Settings option "Console: History Size" to adjust the number of lines stored in the console history when running packages. Defaults to 9001 lines.
#### Inference
- Added new project type, "Flux Text to Image", a Flux-native workflow for text-to-image projects
- Added support for the FP8 version of Flux in the regular Text to Image and Image to Image workflows via the "Use Flux Guidance" Sampler Addon
#### Model Browser
- Added AuraFlow & Flux base model types to the CivitAI model browser
- Added CLIP/Text Encoders section to HuggingFace model browser
#### Checkpoint Manager
- Added new Metadata Editor (accessible via the right-click menu), allowing you to create or edit metadata for models 
- Added "New Directory" and "Delete" options to the context menu of the tree view.
- Added new toggle for drag & drop - when enabled, all selected models will now move together with the dragged model
- Added "File Size" sorting option
- Added "Hide Empty Categories" toggle
- Added "Select All" button to the InfoBar (shown when at least one model is selected)
- Added "unet" shared model folder for ComfyUI
### Changed
- Optimized image loading across the app, with loading speed now up to 4x faster for local images, and up to 17x faster for remote images
- Image loading in the Outputs page now uses native memory management for ~2x less peak memory usage, and will release memory more quickly when switching away from the Outputs page or scrolling images out of view
- Improved animation fluidity of image rendering while scrolling quickly across large collections (e.g. Outputs, Model Browser)
- The "Download Failed" message for model downloads is now persistent until dismissed
- Separated the Generate button from the prompt control in Inference so it can be moved like other controls
### Fixed
- Fixed "The version of the native libSkiaSharp library (88.1) is incompatible with this version of SkiaSharp." error for Linux users
- Fixed download links for IPAdapters in the HuggingFace model browser
- Fixed potential memory leak of transient controls (Inference Prompt and Output Image Viewer) not being garbage collected due to event subscriptions
- Fixed Batch Count seeds not being recorded properly in Inference projects and image metadata
### Supporters
#### Visionaries
- A heartfelt thank you to our Visionary-tier Patreon supporter, **Scopp Mcdee**! We truly appreciate your continued support!

## v2.12.0-dev.2
### Added
- Added Face Detailer module to Inference
- Added ultralytics models to HuggingFace model browser
- Added DoRA category to CivitAI model browser
- Added macOS support for Fooocus & related forks
- (Windows, Linux) Added Vulkan rendering support using launch argument `--vulkan`. (On Windows, the default WinUI composition renderer is likely still preferrable. Linux users are encouraged to try the new renderer to see if it improves performance and responsiveness.)
### Changed
- (Internal) Updated Avalonia to 11.1.1 - Includes major rendering and performance optimizations, animation refinements, improved IME / text selection, and improvements for window sizing / z-order / multi-monitor DPI scaling. ([avaloniaui.net/blog/avalonia-11-1-a-quantum-leap-in-cross-platform-ui-development](https://avaloniaui.net/blog/avalonia-11-1-a-quantum-leap-in-cross-platform-ui-development))
- (Internal) Updated SkiaSharp (Rendering Backend) to 3.0.0-preview.4.1, potentially fixes issues with window rendering artifacts on some machines.
- (Internal) Updated other dependencies for security and bug fixes.
### Fixed
- Fixed some ScrollViewers changing scroll position when focus changes
- Fixed [#782](https://github.com/LykosAI/StabilityMatrix/issues/782) - conflict error when launching new versions of Forge
- Fixed incorrect torch versions being installed for InvokeAI
### Supporters
#### Visionaries
- A huge thank you goes out to our esteemed Visionary-tier Patreon backers: **Scopp Mcdee**, **Waterclouds**, and **Akiro_Senkai**. Your kind support means the world!

## v2.12.0-dev.1
### Added
- Added new package: [Fooocus - mashb1t's 1-Up Edition](https://github.com/mashb1t/Fooocus) by mashb1t
- Added new package: [Stable Diffusion WebUI reForge](https://github.com/Panchovix/stable-diffusion-webui-reForge/) by Panchovix
- Image viewer context menus now have 2 options: `Copy (Ctrl+C)` which now always copies the image as a file, and `Copy as Bitmap (Shift+Ctrl+C)` (Available on Windows) which copies to the clipboard as native bitmap. This changes the previous single `Copy` button behavior that would first attempt a native bitmap copy on Windows when available, and fall back to a file copy if not.
- Added "Change Version" option to the package card overflow menu, allowing you to downgrade or upgrade a package to a specific version or commit
- Added "Disable Update Check" option to the package card overflow menu, allowing you to disable update checks for a specific package
- Added "Run Command" option in Settings for running a command with the embedded Python or Git executables
- Added Intel OneAPI XPU backend (IPEX) option for SD.Next
### Supporters
#### Visionaries
- Shoutout to our Visionary-tier Patreon supporters, **Scopp Mcdee**, **Waterclouds**, and our newest Visionary, **Akiro_Senkai**! Many thanks for your generous support!

## v2.11.8
### Added
- Added Flux & AuraFlow types to CivitAI Browser
- Added unet folder links for ComfyUI thanks to jeremydk
- Added CLIP folder links for Forge
### Changed
- Updated Brazilian Portuguese translations thanks to thiagojramos
### Fixed
- Fixed [#840](https://github.com/LykosAI/StabilityMatrix/issues/840) - CivitAI model browser not loading search results
- Fixed SwarmUI settings being overwritten on launch
- Fixed [#832](https://github.com/LykosAI/StabilityMatrix/issues/832) [#847](https://github.com/LykosAI/StabilityMatrix/issues/847) - Forge output folder links pointing to the incorrect folder
- Fixed errors when downloading models with invalid characters in the file name
- Fixed error when installing RuinedFooocus on nvidia GPUs
### Supporters
#### Pioneers
- A big shoutout to our Pioneer-tier patrons: **tankfox**, **tanangular**, **Mr. Unknown**, and **Szir777**! We deeply appreciate your ongoing support!

## v2.11.7
### Changed
- Forge will use the recommended pytorch version 2.3.1 the next time it is updated
- InvokeAI users with AMD GPUs on Linux will be upgraded to the rocm5.6 version of pytorch the next time it is updated
### Fixed
- Fixed Inference not connecting with "Could not connect to backend - JSON value could not be converted" error with API changes from newer ComfyUI versions
### Supporters
#### Pioneers
- Shoutout to our Pioneer-tier supporters on Patreon: **tankfox**, **tanangular**, **Mr. Unknown**, and **Szir777**! Thanks for all of your continued support!

## v2.11.6
### Fixed
- Fixed incorrect IPAdapter download links in the HuggingFace model browser
- Fixed potential memory leak of transient controls (Inference Prompt and Output Image Viewer) not being garbage collected due to event subscriptions
- Fixed Batch Count seeds not being recorded properly in Inference projects and image metadata
- Fixed [#795](https://github.com/LykosAI/StabilityMatrix/issues/795) - SwarmUI launch args not working properly
- Fixed [#745](https://github.com/LykosAI/StabilityMatrix/issues/745) - not passing Environment Variables to SwarmUI
### Supporters
#### Visionaries
- Shoutout to our Visionary-tier Patreon supporter, **Scopp Mcdee**! Huge thanks for your continued support!
#### Pioneers
- Many thanks to our Pioneer-tier supporters on Patreon: **tankfox**, **tanangular**, **Mr. Unknown**, and **Szir777**! Your continued support is greatly appreciated!

## v2.11.5
### Added
- Added DoRA category to CivitAI model browser
### Fixed
- Fixed `TaskCanceledException` when adding CivitAI Api key or searching for models when the API takes too long to respond. Retry and timeout behavior has been improved.
- Fixed [#782](https://github.com/LykosAI/StabilityMatrix/issues/782) - conflict error when launching new versions of Forge
- Fixed incorrect torch versions being installed for InvokeAI
- Fixed `ArgumentOutOfRangeException` with the Python Packages dialog ItemSourceView when interacting too quickly after loading.
### Supporters
#### Visionaries
- Shoutout to our Visionary-tier Patreon supporters, **Scopp Mcdee**, **Waterclouds**, and our newest Visionary, **Akiro_Senkai**! Many thanks for your generous support!
#### Pioneers
- Many thanks to our Pioneer-tier supporters on Patreon, **tankfox**, **tanangular**, and our newest Pioneers, **Mr. Unknown** and **Szir777**! Your support is greatly appreciated!

## v2.11.4
### Changed
- Base Python install will now use `setuptools==69.5.1` for compatibility with `torchsde`. Individual Packages can upgrade as required.
- Improved formatting of "Copy Details" action on the Unexpected Error dialog
- (Debug) Logging verbosity for classes can now be configured with environment variables (`Logging__LogLevel__<TypeFullName>`).  
### Fixed
- Fixed ComfyUI slower generation speed with new torch versions not including flash attention for windows, pinned `torch==2.1.2` for ComfyUI on Windows CUDA
- Fixed [#719](https://github.com/LykosAI/StabilityMatrix/issues/719) - Fix comments in Inference prompt not being ignored
- Fixed TaskCanceledException when Inference prompts finish before the delayed progress handler (250ms)
### Supporters
#### Visionaries
- Huge thanks to our Visionary-tier supporters on Patreon, **Scopp Mcdee** and **Waterclouds**! Your support helps us continue to improve Stability Matrix!
#### Pioneers
- Thank you to our Pioneer-tier supporters on Patreon, **tankfox** and **tanangular**! Your support is greatly appreciated!

## v2.11.3
### Changed
- Base Python install will now use `pip>=23.3.2,<24.1` for compatibility with `torchsde`.Individual Packages can upgrade as required.
- Added default `PIP_DISABLE_PIP_VERSION_CHECK=1` environment variable to suppress notices about pip version checks.
  - As with other default environment variables, this can be overridden by setting your own value in `Settings > Environment Variables [Edit]`.
### Fixed
- Fooocus Package - Added `pip>=23.3.2,<24.1` specifier before install, fixes potential install errors due to deprecated requirement spec used by `torchsde`.
- Fixed error when launching SwarmUI when installed to a path with spaces
- Fixed issue where model folders were being created too late in certain cases
- Fixed [#683](https://github.com/LykosAI/StabilityMatrix/issues/683) - Model indexing causing LiteDB errors after upgrading from older versions due to updated enum values
### Supporters
#### Visionaries
- Huge thanks to our Visionary-tier supporters on Patreon, **Scopp Mcdee** and **Waterclouds**! Your support helps us continue to improve Stability Matrix!
#### Pioneers
- Thank you to our Pioneer-tier supporters on Patreon, **tankfox** and **tanangular**! Your support is greatly appreciated!

## v2.11.2
### Changed
- StableSwarmUI installs will be migrated to SwarmUI by mcmonkeyprojects the next time the package is updated
  - Note: As of 2024/06/21 StableSwarmUI will no longer be maintained under Stability AI. The original developer will be maintaining an independent version of this project
### Fixed
- Fixed [#700](https://github.com/LykosAI/StabilityMatrix/issues/700) - `cannot import 'packaging'` error for Forge
### Supporters
#### Visionaries
- Huge thanks to our Visionary-tier supporters on Patreon, **Scopp Mcdee** and **Waterclouds**! Your support helps us continue to improve Stability Matrix!
#### Pioneers
- Thank you to our Pioneer-tier supporters on Patreon, **tankfox** and **tanangular**! Your support is greatly appreciated!

## v2.11.1
### Added
- Added Rename option back to the Checkpoints page
### Changed
- Unobserved Task Exceptions across the app will now show a toast notification to aid in debugging
- Updated SD.Next Package details and thumbnail - [#697](https://github.com/LykosAI/StabilityMatrix/pull/697)
### Fixed
- Fixed [#689](https://github.com/LykosAI/StabilityMatrix/issues/689) - New ComfyUI installs encountering launch error due to torch 2.0.0 update, added pinned `numpy==1.26.4` to install and update.
- Fixed Inference image mask editor's 'Load Mask' not able to load image files
- Fixed Fooocus ControlNet default config shared folder mode not taking effect
- Fixed tkinter python libraries not working on macOS with 'Can't find a usable init.tcl' error
### Supporters
#### Visionaries
- Shoutout to our Visionary-tier supporters on Patreon, **Scopp Mcdee** and **Waterclouds**! Your generous support is appreciated and helps us continue to make Stability Matrix better for everyone!
#### Pioneers
- A big thank you to our Pioneer-tier supporters on Patreon, **tankfox** and **tanangular**! Your support helps us continue to improve Stability Matrix!

## v2.11.0
### Added
#### Packages
- Added new package: [SDFX](https://github.com/sdfxai/sdfx/) by sdfxai
- Added ZLUDA option for SD.Next
- Added more launch options for Forge - [#618](https://github.com/LykosAI/StabilityMatrix/issues/618)
- Added search bar to the Python Packages dialog
#### Inference
- Added Inpainting support for Image To Image projects using the new image mask canvas editor
- Added alternate Lora / LyCORIS drop-down model selection, can be toggled via the model settings button. Allows choosing both CLIP and Model Weights. The existing prompt-based `<lora:model:1.0>` method is still available.
- Added optional Recycle Bin mode when deleting images in the Inference image browser, can be disabled in settings (Currently available on Windows and macOS)
#### Model Browsers
- Added PixArt, SDXL Hyper, and SD3 options to the CivitAI Model Browser
- Added XL ControlNets section to HuggingFace model browser
- Added download speed indicator to model downloads in the Downloads tab
#### Output Browser
- Added support for indexing and displaying jpg/jpeg & gif images (in additional to png and webp / animated webp), with metadata parsing and search for compatible formats
#### Settings
- Added setting for locale specific or invariant number formatting
- Added setting for toggling model browser auto-search on load
- Added option in Settings to choose whether to Copy or Move files when dragging and dropping files into the Checkpoint Manager
- Added folder shortcuts in Settings for opening common app and system folders, such as Data Directory and Logs
#### Translations
- Added Brazilian Portuguese language option, thanks to jbostroski for the translation!
### Changed
- Maximized state is now stored on exit and restored on launch
- Drag & drop imports now move files by default instead of copying
- Clicking outside the Select Model Version dialog will now close it
- Changed Package card buttons to better indicate that they are buttons
- Log file storage has been moved from `%AppData%/StabilityMatrix` to a subfolder: `%AppData%/StabilityMatrix/Logs`
- Archived log files now have an increased rolling limit of 9 files, from 2 files previously. Their file names will now be in the format `app.{yyyy-MM-dd HH_mm_ss}.log`. The current session log file remains named `app.log`.
- Updated image controls on Recommended Models dialog to match the rest of the app
- Improved app shutdown clean-up process reliability and speed
- Improved ProcessTracker speed and clean-up safety for faster subprocess and package launching performance
- Updated HuggingFace page so the command bar stays fixed at the top
- Revamped Checkpoints page now shows available model updates and has better drag & drop functionality
- Revamped file deletion confirmation dialog with affected file paths display and recycle bin / permanent delete options (Checkpoint and Output Browsers) (Currently available on Windows and macOS)
### Fixed
- Fixed crash when parsing invalid generated images in Output Browser and Inference image viewer, errors will be logged instead and the image will be skipped
- Fixed missing progress text during package updates
- (Windows) Fixed "Open in Explorer" buttons across the app not opening the correct path on ReFS partitions
- (macOS, Linux) Fixed Subprocesses of packages sometimes not being closed when the app is closed
- Fixed Inference tabs sometimes not being restored from previous sessions
- Fixed multiple log files being archived in a single session, and losing some log entries
- Fixed error when installing certain packages with comments in the requirements file
- Fixed error when deleting Inference browser images in a nested project path with recycle bin mode
- Fixed extra text in positive prompt when loading image parameters in Inference with empty negative prompt value
- Fixed NullReferenceException that sometimes occurred when closing Inference tabs with images due to Avalonia.Bitmap.Size accessor issue
- Fixed [#598](https://github.com/LykosAI/StabilityMatrix/issues/598) - program not exiting after printing help or version text
- Fixed [#630](https://github.com/LykosAI/StabilityMatrix/issues/630) - InvokeAI update hangs forever waiting for input
- Fixed issue where the "installed" state on HuggingFace model browser was not always correct
- Fixed model folders not being created on startup

### Supporters
#### Visionaries
- Shoutout to our Visionary-tier supporters on Patreon, **Scopp Mcdee** and **Waterclouds**! Your generous support is appreciated and helps us continue to make Stability Matrix better for everyone!
#### Pioneers
- A big thank you to our Pioneer-tier supporters on Patreon, **tankfox** and **tanangular**! Your support helps us continue to improve Stability Matrix!

## v2.11.0-pre.2
### Added
- Added folder shortcuts in Settings for opening common app and system folders, such as Data Directory and Logs.
### Changed
- Log file storage have been moved from `%AppData%/StabilityMatrix` to a subfolder: `%AppData%/StabilityMatrix/Logs`
- Archived log files now have an increased rolling limit of 9 files, from 2 files previously. Their file names will now be in the format `app.{yyyy-MM-dd HH_mm_ss}.log`. The current session log file remains named `app.log`.
- Updated image controls on Recommended Models dialog to match the rest of the app
- Improved app shutdown clean-up process reliability and speed
- Improved ProcessTracker speed and clean-up safety for faster subprocess and package launching performance
### Fixed
- Fixed crash when parsing invalid generated images in Output Browser and Inference image viewer, errors will be logged instead and the image will be skipped
- Fixed issue where blue and red color channels were swapped in the mask editor dialog
- Fixed missing progress text during package updates
- Fixed "Git and Node.js are required" error during SDFX install
- (Windows) Fixed "Open in Explorer" buttons across the app not opening the correct path on ReFS partitions
- (Windows) Fixed Sdfx electron window not closing when stopping the package
- (macOS, Linux) Fixed Subprocesses of packages sometimes not being closed when the app is closed
- Fixed Inference tabs sometimes not being restored from previous sessions
- Fixed multiple log files being archived in a single session, and losing some log entries
- Fixed error when installing certain packages with comments in the requirements file
- Fixed some more missing progress texts during various activities
### Supporters
#### Visionaries
- A heartfelt thank you to our Visionary-tier Patreon supporters, **Scopp Mcdee** and **Waterclouds**! Your generous contributions enable us to keep enhancing Stability Matrix!

## v2.11.0-pre.1
### Added
- Added new package: [SDFX](https://github.com/sdfxai/sdfx/) by sdfxai
- Added "Show Nested Models" toggle for new Checkpoints page, allowing users to show or hide models in subfolders of the selected folder
- Added ZLUDA option for SD.Next
- Added PixArt & SDXL Hyper options to the Civitai model browser
- Added release date to model update notification card on the Checkpoints page
- Added option in Settings to choose whether to Copy or Move files when dragging and dropping files into the Checkpoint Manager
- Added more launch options for Forge - [#618](https://github.com/LykosAI/StabilityMatrix/issues/618)
#### Inference
- Added Inpainting support for Image To Image projects using the new image mask canvas editor
### Changed
- Maximized state is now stored on exit and restored on launch
- Clicking outside the Select Model Version dialog will now close it
- Changed Package card buttons to better indicate that they are buttons
### Fixed
- Fixed error when deleting Inference browser images in a nested project path with recycle bin mode
- Fixed extra text in positive prompt when loading image parameters in Inference with empty negative prompt value
- Fixed NullReferenceException that sometimes occured when closing Inference tabs with images due to Avalonia.Bitmap.Size accessor issue
- Fixed package installs not showing any progress messages
- Fixed crash when viewing model details for Unknown model types in the Checkpoint Manager
- Fixed [#598](https://github.com/LykosAI/StabilityMatrix/issues/598) - program not exiting after printing help or version text
- Fixed [#630](https://github.com/LykosAI/StabilityMatrix/issues/630) - InvokeAI update hangs forever waiting for input
### Supporters
#### Visionaries
- Many thanks to our Visionary-tier supporters on Patreon, **Scopp Mcdee** and **Waterclouds**! Your generous support helps us continue to improve Stability Matrix!

## v2.11.0-dev.3
### Added
- Added download speed indicator to model downloads in the Downloads tab
- Added XL ControlNets section to HuggingFace model browser
- Added toggle in Settings for model browser auto-search on load
- Added optional Recycle Bin mode when deleting images in the Inference image browser, can be disabled in settings (Currently on Windows only)
### Changed
- Revamped Checkpoints page now shows available model updates and has better drag & drop functionality
- Updated HuggingFace page so the command bar stays fixed at the top
- Revamped file deletion confirmation dialog with affected file paths display and recycle bin / permanent delete options (Checkpoint and Output Browsers) (Currently on Windows only)
### Fixed
- Fixed issue where the "installed" state on HuggingFace model browser was not always correct
### Supporters
#### Visionaries
- Special shoutout to our first two Visionaries on Patreon, **Scopp Mcdee** and **Waterclouds**! Thank you for your generous support!

## v2.11.0-dev.2
### Added
- Added Brazilian Portuguese language option, thanks to jbostroski for the translation!
- Added setting for locale specific or invariant number formatting
- Added support for jpg/jpeg & gif images in the Output Browser
### Changed
- Centered OpenArt browser cards
### Fixed
- Fixed MPS install on macOS for ComfyUI, A1111, SDWebUI Forge, and SDWebUI UX causing torch to be upgraded to dev nightly versions and causing incompatibilities with dependencies.
- Fixed "Auto Scroll to End" not working in some scenarios
- Fixed "Auto Scroll to End" toggle button not scrolling to the end when toggled on
- Fixed/reverted output folder name changes for Automatic1111
- Fixed xformers being uninstalled with every ComfyUI update
- Fixed Inference Lora menu strength resetting to default if out of slider range (0 to 1)
- Fixed missing progress text during package installs

## v2.11.0-dev.1
### Added
- Added search bar to the Python Packages dialog
#### Inference
- Alternate Lora / LyCORIS drop-down model selection, can be toggled via the model settings button. The existing prompt-based Lora / LyCORIS method is still available.
### Fixed
- Fixed crash when failing to parse Python package details

## v2.10.3
### Changed
- Centered OpenArt browser cards
### Fixed
- Fixed MPS install on macOS for ComfyUI, A1111, SDWebUI Forge, and SDWebUI UX causing torch to be upgraded to dev nightly versions and causing incompatibilities with dependencies.
- Fixed crash when failing to parse Python package details
- Fixed "Auto Scroll to End" not working in some scenarios
- Fixed "Auto Scroll to End" toggle button not scrolling to the end when toggled on
- Fixed/reverted output folder name changes for Automatic1111
- Fixed xformers being uninstalled with every ComfyUI update
- Fixed missing progress text during package installs

## v2.10.2
### Changed
- Updated translations for Spanish and Turkish
### Fixed
- Fixed more crashes when loading invalid connected model info files
- Fixed pip installs not parsing comments properly
- Fixed crash when sending input to a process that isn't running
- Fixed breadcrumb on console page showing incorrect running package name
- Fixed [#576](https://github.com/LykosAI/StabilityMatrix/issues/576) - drag & drop crashes on macOS & Linux
- Fixed [#594](https://github.com/LykosAI/StabilityMatrix/issues/594) - missing thumbnails in Inference model selector
- Fixed [#600](https://github.com/LykosAI/StabilityMatrix/issues/600) - kohya_ss v24+ not launching
- Downgraded Avalonia back to 11.0.9 to fix [#589](https://github.com/LykosAI/StabilityMatrix/issues/589) and possibly other rendering issues

## v2.10.1
### Added
- Added SVD Shared Model & Output Folders for Forge (fixes [#580](https://github.com/LykosAI/StabilityMatrix/issues/580))
### Changed
- Improved error message when logging in with a Lykos account fails due to incorrect email or password
- Model Browser & Workflow Browser now auto-load when first navigating to those pages
- Removed update confirmation dialog, instead showing the new version in the update button tooltip
### Fixed
- Fixed package launch not working when environment variable `SETUPTOOLS_USE_DISTUTILS` is set due to conflict with a default environment variable. User environment variables will now correctly override any default environment variables.
- Fixed "No refresh token found" error when failing to login with Lykos account in some cases
- Fixed blank entries appearing in the Categories dropdown on the Checkpoints page
- Fixed crash when loading invalid connected model info files
- Fixed [#585](https://github.com/LykosAI/StabilityMatrix/issues/585) - Crash when drag & drop source and destination are the same
- Fixed [#584](https://github.com/LykosAI/StabilityMatrix/issues/584) - `--launch-package` argument not working
- Fixed [#581](https://github.com/LykosAI/StabilityMatrix/issues/581) - Inference teaching tip showing more often than it should
- Fixed [#578](https://github.com/LykosAI/StabilityMatrix/issues/578) - "python setup.py egg_info did not run successfully" failure when installing Auto1111 or SDWebUI Forge
- Fixed [#574](https://github.com/LykosAI/StabilityMatrix/issues/574) - local images not showing on macOS or Linux

## v2.10.0
### Added
- Added Reference-Only mode for Inference ControlNet, used for guiding the sampler with an image without a pretrained model. Part of the latent and attention layers will be connected to the reference image, similar to Image to Image or Inpainting.
- Inference ControlNet module now supports over 42 preprocessors, a new button next to the preprocessors dropdown allows previewing the output of the selected preprocessor on the image.
- Added resolution selection for Inference ControlNet module, this controls preprocessor resolution too.
- Added Layer Diffuse sampler addon to Inference, allows generating foreground with transparency with SD1.5 and SDXL.
- Added support for deep links from the new Stability Matrix Chrome extension
- Added OpenArt.AI workflow browser for ComfyUI workflows
- Added more metadata to the image dialog info flyout
- Added Output Sharing toggle in Advanced Options during install flow
### Changed
- Revamped the Packages page to enable running multiple packages at the same time
- Changed the Outputs Page to use a TreeView for the directory selection instead of a dropdown selector
- Model download location selector now searches all subfolders
- Inference Primary Sampler Addons (i.e. ControlNet, FreeU) are now inherited by Hires Fix Samplers, this can be overriden from the Hires Fix module's settings menu by disabling the "Inherit Primary Sampler Addons" option.
- Revisited the way images are loaded on the outputs page, with improvements to loading speed & not freezing the UI while loading
- Updated translations for French, Spanish, and Turkish
- Changed to a new image control for pages with many images
- (Internal) Updated to Avalonia 11.0.10
### Fixed
- Fixed [#559](https://github.com/LykosAI/StabilityMatrix/issues/559) - "Unable to load bitmap from provided data" error in Checkpoints page
- Fixed [#522](https://github.com/LykosAI/StabilityMatrix/issues/522) - Incorrect output directory path for latest Auto1111
- Fixed [#529](https://github.com/LykosAI/StabilityMatrix/issues/529) - OneTrainer requesting input during update
- Fixed Civitai model browser error when sorting by Installed with more than 100 installed models
- Fixed CLIP Install errors due to setuptools distutils conflict, added default environment variable setting `SETUPTOOLS_USE_DISTUTILS=stdlib`
- Fixed progress bars not displaying properly during package installs & updates
- Fixed ComfyUI extension updates not running install.py / updating requirements.txt
- Improved performance when deleting many images from the Outputs page
- Fixed ComfyUI torch downgrading to 2.1.2 when updating
- Fixed Inference HiresFix module "Inherit Primary Sampler Addons" setting not effectively disabling when unchecked
- Fixed model download location options for VAEs in the CivitAI Model Browser
### Removed
- Removed the main Launch page, as it is no longer needed with the new Packages page

## v2.10.0-pre.2
### Added
- Added more metadata to the image dialog info flyout
- Added Restart button to console page
### Changed
- Model download location selector now searches all subfolders
### Fixed
- Fixed Civitai model browser not showing images when "Show NSFW" is disabled
- Fixed crash when Installed Workflows page is opened with no Workflows folder
- Fixed progress bars not displaying properly during package installs & updates
- Fixed ComfyUI extension updates not running install.py / updating requirements.txt

## v2.10.0-pre.1
### Added
- Added OpenArt.AI workflow browser for ComfyUI workflows
- Added Output Sharing toggle in Advanced Options during install flow
### Changed
- Changed to a new image control for pages with many images
- Removed Symlink option for InvokeAI due to changes with InvokeAI v4.0+
- Output sharing is now enabled by default for new installations
- (Internal) Updated to Avalonia 11.0.10
### Fixed
- Improved performance when deleting many images from the Outputs page
- Fixed ComfyUI torch downgrading to 2.1.2 when updating
- Fixed [#529](https://github.com/LykosAI/StabilityMatrix/issues/529) - OneTrainer requesting input during update
- Fixed "Could not find entry point for InvokeAI" error on InvokeAI v4.0+

## v2.10.0-dev.3
### Added
- Added support for deep links from the new Stability Matrix Chrome extension
### Changed
- Due to changes on the CivitAI API, you can no longer select a specific page in the CivitAI Model Browser
- Due to the above API changes, new pages are now loaded via "infinite scrolling"
### Fixed
- Fixed Inference HiresFix module "Inherit Primary Sampler Addons" setting not effectively disabling when unchecked
- Fixed model download location options for VAEs in the CivitAI Model Browser
- Fixed crash on startup when library directory is not set
- Fixed One-Click install progress dialog not disappearing after completion
- Fixed ComfyUI with Inference pop-up during one-click install appearing below the visible scroll area
- Fixed no packages being available for one-click install on PCs without a GPU
- Fixed models not being removed from the installed models cache when deleting them from the Checkpoints page
- Fixed missing ratings on some models in the CivitAI Model Browser
- Fixed missing favorite count in the CivitAI Model Browser
- Fixed recommended models not showing all SDXL models

## v2.10.0-dev.2
### Added
- Added Reference-Only mode for Inference ControlNet, used for guiding the sampler with an image without a pretrained model. Part of the latent and attention layers will be connected to the reference image, similar to Image to Image or Inpainting.
### Changed
- Inference Primary Sampler Addons (i.e. ControlNet, FreeU) are now inherited by Hires Fix Samplers, this can be overriden from the Hires Fix module's settings menu by disabling the "Inherit Primary Sampler Addons" option.
- Revisited the way images are loaded on the outputs page, with improvements to loading speed & not freezing the UI while loading
### Fixed
- Fixed Outputs page not remembering where the user last was in the TreeView in certain circumstances
- Fixed Inference extension upgrades not being added to missing extensions list for prompted install
- Fixed "The Open Web UI button has moved" teaching tip spam

## v2.10.0-dev.1
### Added
- Inference ControlNet module now supports over 42 preprocessors, a new button next to the preprocessors dropdown allows previewing the output of the selected preprocessor on the image.
- Added resolution selection for Inference ControlNet module, this controls preprocessor resolution too.
### Changed
- Revamped the Packages page to enable running multiple packages at the same time
- Changed the Outputs Page to use a TreeView for the directory selection instead of a dropdown selector
### Removed
- Removed the main Launch page, as it is no longer needed with the new Packages page

## v2.9.3
### Changed
- Removed Symlink option for InvokeAI to prevent InvokeAI from moving models into its own directories (will be replaced with a Config option in a future update)
### Fixed
- Fixed images not appearing in Civitai Model Browser when "Show NSFW" was disabled
- Fixed [#556](https://github.com/LykosAI/StabilityMatrix/issues/556) - "Could not find entry point for InvokeAI" error

## v2.9.2
### Changed
- Due to changes with the CivitAI API, you can no longer select a specific page in the CivitAI Model Browser
- Due to the above API changes, new pages are now loaded via "infinite scrolling"
### Fixed
- Fixed models not being removed from the installed models cache when deleting them from the Checkpoints page
- Fixed model download location options for VAEs in the CivitAI Model Browser
- Fixed One-Click install progress dialog not disappearing after completion
- Fixed ComfyUI with Inference pop-up during one-click install appearing below the visible scroll area
- Fixed no packages being available for one-click install on PCs without a GPU

## v2.9.1
### Added
- Fixed [#498](https://github.com/LykosAI/StabilityMatrix/issues/498) Added "Pony" category to CivitAI Model Browser
### Changed
- Changed package deletion warning dialog to require additional confirmation
### Fixed
- Fixed [#502](https://github.com/LykosAI/StabilityMatrix/issues/502) - missing launch options for Forge
- Fixed [#500](https://github.com/LykosAI/StabilityMatrix/issues/500) - missing output images in Forge when using output sharing
- Fixed [#490](https://github.com/LykosAI/StabilityMatrix/issues/490) - `mpmath has no attribute 'rational'` error on macOS
- Fixed [#510](https://github.com/ionite34/StabilityMatrix/pull/564/files) - kohya_ss packages with v23.0.x failing to install due to missing 'packaging' dependency
- Fixed incorrect progress text when deleting a checkpoint from the Checkpoints page
- Fixed incorrect icon colors on macOS

## v2.9.0
### Added
- Added new package: [StableSwarmUI](https://github.com/Stability-AI/StableSwarmUI) by Stability AI
- Added new package: [Stable Diffusion WebUI Forge](https://github.com/lllyasviel/stable-diffusion-webui-forge) by lllyasviel
- Added extension management for SD.Next and Stable Diffusion WebUI-UX
- Added the ability to choose where CivitAI model downloads are saved
- Added `--launch-package` argument to launch a specific package on startup, using display name or package ID (i.e. `--launch-package "Stable Diffusion WebUI Forge"` or `--launch-package c0b3ecc5-9664-4be9-952d-a10b3dcaee14`)
- Added more Base Model search options to the CivitAI Model Browser
- Added Stable Cascade to the HuggingFace Model Browser
#### Inference
- Added Inference Prompt Styles, with Prompt Expansion model support (i.e. Fooocus V2)
- Added option to load a .yaml config file next to the model with the same name. Can be used with VPred and other models that require a config file.
- Added copy image support on linux and macOS for Inference outputs viewer menu
### Changed
- Updated translations for German, Spanish, French, Japanese, Portuguese, and Turkish
- (Internal) Updated to Avalonia 11.0.9
### Fixed
- Fixed StableSwarmUI not installing properly on macOS
- Fixed [#464](https://github.com/LykosAI/StabilityMatrix/issues/464) - error when installing InvokeAI on macOS
- Fixed [#335](https://github.com/LykosAI/StabilityMatrix/issues/335) Update hanging indefinitely after git step for Auto1111 and SDWebUI Forge
- Fixed Inference output viewer menu "Copy" not copying image
- Fixed image viewer dialog arrow key navigation not working
- Fixed CivitAI login prompt not showing when downloading models that require CivitAI logins
- Fixed unknown model types not showing on checkpoints page (thanks Jerry!)
- Improved error handling for Inference Select Image hash calculation in case file is being written to while being read

## v2.9.0-pre.2
### Added
- Added `--launch-package` argument to launch a specific package on startup, using display name or package ID (i.e. `--launch-package "Stable Diffusion WebUI Forge"` or `--launch-package c0b3ecc5-9664-4be9-952d-a10b3dcaee14`)
- Added more Base Model search options to the CivitAI Model Browser
- Added Stable Cascade to the HuggingFace Model Browser
### Changed
- (Internal) Updated to Avalonia 11.0.9
### Fixed
- Fixed image viewer dialog arrow key navigation not working
- Fixed CivitAI login prompt not showing when downloading models that require CivitAI logins

## v2.9.0-pre.1
### Added
- Added Inference Prompt Styles, with Prompt Expansion model support (i.e. Fooocus V2)
- Added copy image support on linux and macOS for Inference outputs viewer menu
### Fixed
- Fixed StableSwarmUI not installing properly on macOS
- Fixed output sharing for Stable Diffusion WebUI Forge
- Hopefully actually fixed [#464](https://github.com/LykosAI/StabilityMatrix/issues/464) - error when installing InvokeAI on macOS
- Fixed default command line args for SDWebUI Forge on macOS
- Fixed output paths and output sharing for SDWebUI Forge
- Maybe fixed update hanging for Auto1111 and SDWebUI Forge
- Fixed Inference output viewer menu "Copy" not copying image 

## v2.9.0-dev.2
### Added
#### Inference
- Added option to load a .yaml config file next to the model with the same name. Can be used with VPred and other models that require a config file.
### Fixed
- Fixed icon sizes of Inference Addons and Steps buttons

## v2.9.0-dev.1
### Added
- Added new package: [StableSwarmUI](https://github.com/Stability-AI/StableSwarmUI) by Stability AI
- Added new package: [Stable Diffusion WebUI Forge](https://github.com/lllyasviel/stable-diffusion-webui-forge) by lllyasviel
- Added extension management for SD.Next and Stable Diffusion WebUI-UX
- Added the ability to choose where CivitAI model downloads are saved

## v2.8.4
### Fixed
- Hopefully actually fixed [#464](https://github.com/LykosAI/StabilityMatrix/issues/464) - error when installing InvokeAI on macOS

## v2.8.3
### Fixed
- Fixed user tokens read error causing failed downloads
- Failed downloads will now log error messages
- Fixed [#458](https://github.com/LykosAI/StabilityMatrix/issues/458) - Save Intermediate Image not working
- Fixed [#453](https://github.com/LykosAI/StabilityMatrix/issues/453) - Update Fooocus `--output-directory` argument to `--output-path`

## v2.8.2
### Added
- Added missing GFPGAN link to Automatic1111 packages
### Fixed
- Fixed Inference Image to Image Denoise setting becoming hidden after changing schedulers
- Fixed Inference ControlNet models showing as downloadable even when they are already installed
- Fixed Inference Sampler Addon conditioning not applying (i.e. ControlNet)
- Fixed extension modification dialog not showing any progress messages

## v2.8.1
### Fixed
- Fixed model links not working in RuinedFooocus for new installations
- Fixed incorrect nodejs download link on Linux (thanks to slogonomo for the fix)
- Fixed failing InvokeAI install on macOS due to missing nodejs
- Increased timeout on Recommended Models call to prevent potential timeout errors on slow connections
- Fixed SynchronizationLockException when saving settings
- Improved error messages with process output for 7z extraction errors
- Fixed missing tkinter dependency for OneTrainer on Windows
- Fixed auto-update on macOS not starting new version from an issue in starting .app bundles with arguments
- Fixed [#436](https://github.com/LykosAI/StabilityMatrix/issues/436) - Crash on invalid json files during checkpoint indexing

## v2.8.0
### Added
- Added Image to Video project type
- Added CLIP Skip setting to inference, toggleable from the model settings button
- Added image and model details in model selection boxes
- Added new package: [OneTrainer](https://github.com/Nerogar/OneTrainer)
- Added native desktop push notifications for some events (i.e. Downloads, Package installs, Inference generation)
  - Currently available on Windows and Linux, macOS support is pending
- Added Package Extensions (Plugins) management - accessible from the Packages' 3-dot menu. Currently supports ComfyUI and Automatic1111.
- Added new launch argument options for Fooocus
- Added "Config" Shared Model Folder option for Fooocus
- Added Recommended Models dialog after one-click installer
- Added "Copy Details" button to Unexpected Error dialog
- Added German language option, thanks to Mario da Graca for the translation
- Added Portuguese language options, thanks to nextosai for the translation
- Added base model filter to Checkpoints page
- Added "Compatible Images" category when selecting images for Inference projects
- Added "Find in Model Browser" option to the right-click menu on the Checkpoints page
- Added `--use-directml` launch argument for SDWebUI DirectML fork
- Added release builds for macOS (Apple Silicon)
- Added ComfyUI launch argument configs: Cross Attention Method, Force Floating Point Precision, VAE Precision
- Added Delete button to the CivitAI Model Browser details dialog
- Added "Copy Link to Clipboard" for connected models in the Checkpoints page
- Added support for webp files to the Output Browser
- Added "Send to Image to Image" and "Send to Image to Video" options to the context menu
### Changed
- New package installation flow
- Changed one-click installer to match the new package installation style
- Automatic1111 packages will now use PyTorch v2.1.2. Upgrade will occur during the next package update or upon fresh installation.
- Search box on Checkpoints page now searches tags and trigger words
- Changed the Close button on the package install dialog to "Hide"
  - Functionality remains the same, just a name change
- Updated translations for the following languages:
  - Spanish
  - French
  - Japanese
  - Turkish
- Inference file name patterns with directory separator characters will now have the subdirectories created automatically
- Changed how settings file is written to disk to reduce potential data loss risk
- (Internal) Updated to Avalonia 11.0.7
### Fixed
- Fixed error when ControlNet module image paths are not found, even if the module is disabled
- Fixed error when finding metadata for archived models
- Fixed error when extensions folder is missing
- Fixed crash when model was not selected in Inference
- Fixed Fooocus Config shared folder mode overwriting unknown config keys
- Fixed potential SD.Next update issues by moving to shared update process
- Fixed crash on startup when Outputs page failed to load categories properly
- Fixed image gallery arrow key navigation requiring clicking before responding
- Fixed crash when loading extensions list with no internet connection
- Fixed crash when invalid launch arguments are passed
- Fixed missing up/downgrade buttons on the Python Packages dialog when the version was not semver compatible


## v2.8.0-pre.5
### Fixed
- Fixed error when ControlNet module image paths are not found, even if the module is disabled
- Fixed error when finding metadata for archived models
- Fixed error when extensions folder is missing
- Fixed error when webp files have incorrect metadata
- Fixed crash when model was not selected in Inference
- Fixed Fooocus Config shared folder mode overwriting unknown config keys

## v2.8.0-pre.4
### Added
- Added Recommended Models dialog after one-click installer
- Added native desktop push notifications for some events (i.e. Downloads, Package installs, Inference generation)
  - Currently available on Windows and Linux, macOS support is pending
- Added settings options for notifications
- Added new launch argument options for Fooocus
- Added Automatic1111 & Stable Diffusion WebUI-UX to the compatible macOS packages
### Changed
- Changed one-click installer to match the new package installation style
- Automatic1111 packages will now use PyTorch v2.1.2. Upgrade will occur during the next package update or upon fresh installation.
- Updated French translation with the latest changes
### Fixed
- Fixed [#413](https://github.com/LykosAI/StabilityMatrix/issues/413) - Environment Variables are editable again
- Fixed potential SD.Next update issues by moving to shared update process
- Fixed Invoke install trying to use system nodejs
- Fixed crash on startup when Outputs page failed to load categories properly

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
