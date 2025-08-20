# StrongTypeResource

:star:  I appreciate your star, it encourages! :star:

[![NuGet](https://img.shields.io/nuget/v/EugeneLepekhin.StrongTypeResource.svg)](https://www.nuget.org/packages/EugeneLepekhin.StrongTypeResource/)
[![.NET C#](https://img.shields.io/badge/.NET-C%23-blue)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

StrongTypeResource is a NuGet package that provides strongly typed access to .NET resources with additional verification of satellite .resx files.

## What It Does

- **Strongly Typed Access**: Generates a class with strongly typed properties and methods that provide safe access to your .resx resources
- **Parameter Validation**: Automatically verify that format parameters match across different culture files
- **Build-Time Safety**: Catch invalid format specifiers and other resource-related errors during compilation instead of runtime

## Example

Given these resources in your .resx file:
```
WelcomeMessage = Welcome to our application
ItemsFound = Found {0} items in {1} seconds
```

**Traditional .NET resource generation** creates properties for both:
```csharp
// Both are properties - no compile-time parameter validation
string message = Resources.WelcomeMessage;
string formatted = string.Format(Resources.ItemsFound, count, time); // Easy to mess up parameters
```

**With StrongTypeResource** (using comment `{int count, double time}` for ItemsFound):
```csharp
// Simple strings remain properties, formatted strings become type-safe methods
string message = Resources.WelcomeMessage;
string formatted = Resources.ItemsFound(count, time); // Compile-time parameter validation
```

Please see [StrongTypeResource package read me](StrongTypeResource/readme.md) for detailed usage instructions.

Here is a NuGet package link: [StrongTypeResource](https://www.nuget.org/packages/EugeneLepekhin.StrongTypeResource/)
