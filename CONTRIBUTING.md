# Stability Matrix Code Style Guidelines
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