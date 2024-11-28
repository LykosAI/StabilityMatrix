# Building
## Running & Debug
- If building using managed IDEs like Rider or Visual Studio, ensure that a valid `--runtime ...` argument is being passed to `dotnet`, or `RuntimeIdentifier=...` is set for calling `msbuild`. This is required for runtime-specific resources to be included in the build. Stability Matrix currently supports building for the `win-x64`, `linux-x64` and `osx-arm64` runtimes.
- You can also build the `StabilityMatrix.Avalonia` project using `dotnet`:
```bash
dotnet build ./StabilityMatrix.Avalonia/StabilityMatrix.Avalonia.csproj -r win-x64 -c Debug
```
- Note that on Windows, the `net8.0-windows10.0.17763.0` framework is used, build outputs will be in `StabilityMatrix.Avalonia/bin/Debug/net8.0-windows10.0.17763.0/win-x64`. On other platforms the `net8.0` framework is used.

## Building to single file for release
(Replace `$RELEASE_VERSION` with a non v-prefixed semver version number, e.g. `2.10.0`, `2.11.0-dev.1`, etc.)
### Windows
```bash
dotnet publish ./StabilityMatrix.Avalonia/StabilityMatrix.Avalonia.csproj -r win-x64 -c Release -p:Version=$env:RELEASE_VERSION -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true
```
### macOS
The `output_dir` environment variable can be specified or defaults to `./out/osx-arm64/`
```bash
./Build/build_macos_app.sh -v $RELEASE_VERSION
```
### Linux
```bash
sudo apt-get -y install libfuse2
dotnet tool install -g KuiperZone.PupNet
pupnet -r linux-x64 -c Release --kind appimage --app-version $RELEASE_VERSION --clean
```

# Scripts
## Install Husky.Net & Pre-commit hooks
- Building the `StabilityMatrix.Avalonia` project once should also install Husky.Net, or run the following command:
```bash
dotnet tool restore && dotnet husky install
```

## Adding Husky pre-commit hooks
```bash
dotnet husky install
```

# Style Guidelines
These are just guidelines, mostly following the official C# style guidelines, except in a few cases. We might not adhere to these 100% ourselves, but lets try our best :)

## Naming conventions
#### Pascal Case
- Use pascal casing ("PascalCasing") when naming a `class`, `record`, `struct`, or `public` members of types, such as fields, properties, methods, and local functions.
- When naming an `interface`, use pascal casing in addition to prefixing the name with the letter `I` to clearly indicate to consumers that it's an `interface`.
#### Camel Case
- Use camel casing ("camelCasing") when naming `private` or `internal` fields.
- **Do not** prefix them with an underscore `_`
## `using` Directives
- Please do not check in code with unused using statements.
## File-scoped Namespaces
- Always use file-scoped namespaces. For example:
```csharp
using System;

namespace X.Y.Z;

class Foo
{
}
```
## Implicitly typed local variables
- Use implicit typing (`var`) for local variables when the type of the variable is obvious from the right side of the assignment, or when the precise type is not important.

## Optional Curly Braces
- Only omit curly braces from `if` statements if the statement immediately following is a `return`. 

For example, the following snippet is acceptable:
```csharp
if (alreadyAteBreakfast)
    return;
```

Otherwise, it must be wrapped in curly braces, like so:
```csharp
if (alreadyAteLunch)
{
    mealsEaten++;
}
```

## Project Structure
- Try to follow our existing structure, such as putting model classes in the `Models\` directory, ViewModels in `ViewModels\`, etc.
- Static classes with only extension methods should be in `Extensions\`
- Mock data for XAML Designer should go in `DesignData\`
- The `Helper\` and `Services\` folder don't really have guidelines, use your best judgment
- XAML & JSON converters should go in the `Converters\` and `Converters\Json\` directories respectively
- Refit interfaces should go in the `Api\` folder
