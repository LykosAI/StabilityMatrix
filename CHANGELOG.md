# Changelog

All notable changes to Stability Matrix will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning 2.0](https://semver.org/spec/v2.0.0.html).

## v2.2.0
### Changed
- Revamped Package Manager UI

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
