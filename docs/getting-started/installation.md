# Installation

Stability Matrix is available for Windows, macOS, and Linux. This page covers how to download and install the application on each platform.

[`Section Overview`](overview.md) | [`Home`](../README.md)

## Table of Contents

- [Download](#download)
- [Windows](#windows)
- [macOS](#macos)
- [Linux](#linux)
- [Portable Mode](#portable-mode)

---

## Download

The two main download sources are the official [Downloads page](https://lykos.ai/downloads) and the project's [GitHub Releases page](https://github.com/LykosAI/StabilityMatrix/releases/latest).

For most users, the Downloads page is the easiest starting point. It exposes the current stable release and also shows preview or development builds when those channels are available. For users who want the latest stable GitHub release directly, the GitHub Releases page is the simplest source.

The current published release artifacts are:

- [Windows x64 `.zip`](https://github.com/LykosAI/StabilityMatrix/releases/latest/download/StabilityMatrix-win-x64.zip)
- [Linux x64 `.zip`](https://github.com/LykosAI/StabilityMatrix/releases/latest/download/StabilityMatrix-linux-x64.zip)
- [macOS Apple Silicon `.dmg`](https://github.com/LykosAI/StabilityMatrix/releases/latest/download/StabilityMatrix-macos-arm64.dmg)

Official releases are portable and self-contained. Separate system-wide installation of Python, Git, or a .NET desktop runtime is not normally required for the packaged builds.

## Windows

Windows releases are distributed as a `.zip` archive rather than a traditional installer.

1. Download the [Windows x64 release `.zip`](https://github.com/LykosAI/StabilityMatrix/releases/latest/download/StabilityMatrix-win-x64.zip).
2. Extract the archive to a folder where Stability Matrix should live.
3. Open the extracted folder and run `StabilityMatrix.exe`.

The Microsoft Visual C++ Redistributable for x64 is required on Windows. Stability Matrix checks for it automatically during package installation and silently installs the required version as part of the normal prerequisite setup, so most users never need to do anything here. If a package still fails to start because the required Microsoft C/C++ runtime is missing (e.g., a missing c10.dll error when loading PyTorch), that's a sign the automatic install didn't complete successfully — as a fallback, install the latest [Visual C++ Redistributable x64 package](https://aka.ms/vc14/vc_redist.x64.exe) manually, or see Microsoft's [Visual C++ Redistributable downloads page](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170).

On first launch, Windows may show a SmartScreen warning because the app was downloaded from the internet. If that happens, select **More info** and then **Run anyway** to continue, provided the download came from the official Downloads page or the project's GitHub Releases page.

For most users, there is no separate runtime setup step. The release build is packaged to run as a self-contained desktop application.

## macOS

Official macOS releases are published for Apple Silicon as a `.dmg`.

1. Download the [macOS Apple Silicon `.dmg`](https://github.com/LykosAI/StabilityMatrix/releases/latest/download/StabilityMatrix-macos-arm64.dmg).
2. Open the downloaded disk image.
3. Drag **Stability Matrix.app** into the **Applications** folder.
4. Launch Stability Matrix from Applications.

If Gatekeeper blocks the first launch, open the app once with **Open** from the context menu, or allow it from **System Settings > Privacy & Security** if macOS shows an override prompt there.

Platform support details and hardware expectations on Apple Silicon (MPS) will be covered in a planned Hardware Support page.

## Linux

Official Linux releases are published as a `.zip` archive that contains the AppImage build.

1. Download the [Linux x64 release `.zip`](https://github.com/LykosAI/StabilityMatrix/releases/latest/download/StabilityMatrix-linux-x64.zip).
2. Extract the archive.
3. Mark the included `StabilityMatrix.AppImage` file as executable.
4. Run the AppImage.

Example commands:

```bash
unzip StabilityMatrix-linux-x64.zip
chmod +x StabilityMatrix.AppImage
./StabilityMatrix.AppImage
```

Depending on the distribution, AppImage runtime support packages may still be required. The current repo and docs specifically call out `libfuse2`, and on some systems `libappimage` or `libxcrypt-compat` may also be needed.

Arch-based users can also use the [AUR package](https://aur.archlinux.org/packages/stabilitymatrix), but it comes with a few practical differences from the standalone AppImage release. The AUR build is typically installed under `/opt`, cannot update itself through Stability Matrix's in-app updater, and instead must be updated through the AUR package as part of normal system package maintenance. There can also be a delay between a new Stability Matrix release and the corresponding `PKGBUILD` update in AUR. If that delay is a problem, or if `/opt` ownership and file-permission behavior causes update or launch issues, the standalone AppImage release is the safer option.

## Portable Mode

Portable Mode keeps the Stability Matrix `Data` directory alongside the application instead of sending it to a separate library location. This is the recommended default for most users because it makes the entire setup easier to move between folders, drives, or systems.

The detailed behavior, default paths, and migration implications are covered more fully in [Data Directory](data-directory.md).

Next step: [First Launch](first-launch.md)
