# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Overview

Stability Matrix is a multi-platform package manager and inference UI for Stable Diffusion, built with C# .NET 9, Avalonia UI, and following MVVM architecture. It manages various AI packages (ComfyUI, Automatic1111, Fooocus, etc.) with embedded Python/Git dependencies and a built-in inference interface.

## Build & Development Commands

### Prerequisites
- .NET 9 SDK
- Platform-specific runtime identifier required: `win-x64`, `linux-x64`, or `osx-arm64`

### Build
```bash
# Debug build (specify runtime)
dotnet build ./StabilityMatrix.Avalonia/StabilityMatrix.Avalonia.csproj -r win-x64 -c Debug
dotnet build ./StabilityMatrix.Avalonia/StabilityMatrix.Avalonia.csproj -r linux-x64 -c Debug
dotnet build ./StabilityMatrix.Avalonia/StabilityMatrix.Avalonia.csproj -r osx-arm64 -c Debug

# Windows uses net8.0-windows10.0.17763.0 framework, output in:
# StabilityMatrix.Avalonia/bin/Debug/net8.0-windows10.0.17763.0/win-x64/

# Other platforms use net8.0 framework, output in:
# StabilityMatrix.Avalonia/bin/Debug/net8.0/{runtime}/
```

### Testing
```bash
# Run all unit tests
dotnet test StabilityMatrix.Tests

# Run UI tests (Windows only)
dotnet test StabilityMatrix.UITests
```

### Code Formatting
```bash
# Install Husky.Net and pre-commit hooks (run once)
dotnet tool restore && dotnet husky install

# Format C# code with CSharpier
dotnet csharpier format

# Format AXAML files with XamlStyler
dotnet xstyler -f <file>
```

### OpenAPI Client Generation
```bash
# Regenerate API clients (Refitter)
dotnet husky run -g generate-openapi
```

### Release Build
```bash
# Single file publish for distribution
dotnet publish ./StabilityMatrix.Avalonia/StabilityMatrix.Avalonia.csproj \
  -r win-x64 -c Release \
  -p:Version=<VERSION> \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishReadyToRun=true

# macOS (requires Build/build_macos_app.sh script)
./Build/build_macos_app.sh -v <VERSION>

# Linux AppImage (requires PupNet)
pupnet -r linux-x64 -c Release --kind appimage --app-version <VERSION> --clean
```

## Architecture

### Project Structure
- **StabilityMatrix.Avalonia**: Main UI application using Avalonia MVVM framework
- **StabilityMatrix.Core**: Business logic, API clients, package management, Python interop
- **StabilityMatrix.Native**: Platform-specific native code abstractions
- **StabilityMatrix.Native.Windows/macOS**: Platform implementations
- **StabilityMatrix.Tests**: Unit tests using MSTest
- **StabilityMatrix.UITests**: UI automation tests
- **analyzers/**: Roslyn analyzers for code quality

### Key Concepts

#### Package System
The package system manages different Stable Diffusion packages through a plugin architecture:
- `BasePackage`: Abstract base defining package interface (download, install, update, run, shutdown)
- `BaseGitPackage`: Git-based package implementation for most packages
- Individual package implementations: `ComfyUI`, `A3WebUI`, `Fooocus`, `SDWebForge`, etc.
- `IPackageFactory`: Factory for instantiating package types
- Each package defines its Python version, torch variants, launch options, and shared folder configuration

#### Shared Folder System
Packages share model files through configurable methods:
- **SharedFolderMethod**: `Symlink` (default), `Configuration`, or `None`
- **SharedFolderType**: Model types like `StableDiffusion`, `Lora`, `VAE`, `Controlnet`, etc.
- **SharedFolderLayout**: Defines which model types map to which package directories
- Configured per-package via `SetupModelFolders()`, `UpdateModelFolders()`, `RemoveModelFolderLinks()`

#### Dependency Injection
The app uses Microsoft.Extensions.DependencyInjection:
- Service registration in `App.axaml.cs` via `ConfigureServices()`
- Attribute-based registration: `[Singleton]`, `[Transient]` from StabilityMatrix.Core.Attributes
- ViewModels registered through `ConfigurePageViewModels()`
- `IServiceManager<ViewModelBase>` for managing page ViewModels
- MessagePipe for event bus and inter-process communication

#### MVVM Architecture
- **ViewModels**: Inherit from `ViewModelBase`, use CommunityToolkit.Mvvm for commands/properties
- **Views**: AXAML files with compiled bindings (AvaloniaUseCompiledBindingsByDefault=true)
- **Services**: Injected into ViewModels, handle business logic
- **DesignData**: Mock data for XAML designer previews

#### Inference System
Built-in Stable Diffusion inference UI:
- Card-based modular UI system (`StackCardViewModel`, `InferenceTabViewModelBase`)
- Workspaces saved as `.smproj` project files
- Metadata embedded in generated images (ComfyUI nodes, A1111 metadata)
- Custom prompt language with syntax highlighting (`ImagePrompt.tmLanguage.json`)
- Integration with local package installs or remote ComfyUI instances

#### Database
- LiteDB for local storage (`ILiteDbContext`)
- Models: `InstalledPackage`, `LocalModelFile`, settings, etc.
- Repositories handle CRUD operations

#### Model Management
- `IModelIndexService`: Indexes and tracks models across packages
- `ITrackedDownloadService`: Manages downloads from CivitAI, HuggingFace
- Metadata extraction and preview generation
- Checkpoint browser with filtering/search

### Code Patterns

#### Naming Conventions
- PascalCase: classes, records, structs, public members
- camelCase: private/internal fields (NO underscore prefix)
- Interfaces: `I` prefix (e.g., `IPackageFactory`)
- File-scoped namespaces always

#### Project Organization
- `Models/`: Data models and DTOs
- `ViewModels/`: MVVM ViewModels
- `Services/`: Business logic services
- `Api/`: Refit API interfaces
- `Extensions/`: Extension methods only
- `Converters/`: XAML and JSON converters
- `Helper/`: Utilities and helpers
- `DesignData/`: XAML designer mock data

#### ViewModels
- PageViewModels inherit from `PageViewModelBase` and use `[ManagedService]` attribute
- DialogViewModels inherit from `ContentDialogViewModelBase` or `TaskDialogViewModelBase`
- Implement proper disposal via `DisposableViewModelBase` for resources
- Use `LoadableViewModelBase` for async initialization

#### Platform-Specific Code
- Check `Compat.IsWindows`, `Compat.IsLinux`, `Compat.IsMacOS`, `Compat.IsArm`
- Platform services injected based on platform (e.g., `IPrerequisiteHelper`)
- Native interop through StabilityMatrix.Native abstractions

### Adding New Packages
1. Create class in `StabilityMatrix.Core/Models/Packages/` inheriting `BaseGitPackage`
2. Implement required properties: `Name`, `DisplayName`, `Author`, `LaunchCommand`, etc.
3. Define `SharedFolderLayout` for model folder mappings
4. Override `InstallPackage()` for custom setup if needed
5. Register in `PackageFactory.cs`
6. Add launch options via `LaunchOptions` property

### Testing Strategy
- Unit tests use MSTest framework
- Mock dependencies with NSubstitute
- UI tests use Avalonia's headless testing
- `InternalsVisibleTo` enables testing internal members

## Important Files
- `.editorconfig`: Code style rules (120 char line length, var usage)
- `.csharpierrc.yaml`: C# formatter config
- `Directory.Packages.props`: Centralized package version management
- `ConditionalSymbols.props`: Conditional compilation symbols
- `global.json`: .NET SDK version pinning (9.0.0)
- `.husky/task-runner.json`: Pre-commit hooks and tasks
