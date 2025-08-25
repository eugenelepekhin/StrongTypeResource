# StrongTypeResource

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

## Getting Started

### 1. Install the Package
Add the StrongTypeResource NuGet package to your project.

### 2. Configure Your Resource Files
Replace the default Custom Tool for your .resx files with one of these generators:
- `MSBuild:StrongTypeResourceInternal` - Creates an internal class
- `MSBuild:StrongTypeResourcePublic` - Creates a public class (useful for cross-project access and WPF binding)

> If you have been using older versions of StrongTypeResource, you will need to replace old Custom Tool `StrongTypeResource.internal` or `StrongTypeResource.public`
with `MSBuild:StrongTypeResourceInternal` or `MSBuild:StrongTypeResourcePublic`. You may need to restart Visual Studio after changing the Custom Tool.

The new generator strings allow to generate the wrapper code after you save the .resx file, so you can see changes in intellisense immediately without rebuilding the project.

#### Option A: Using Visual Studio
1. Right-click your .resx file
2. Select **Properties**
3. Change **Custom Tool** to `MSBuild:StrongTypeResourceInternal` or `MSBuild:StrongTypeResourcePublic`

#### Option B: Edit .csproj Directly
```xml
<ItemGroup>
  <EmbeddedResource Update="Resources\Text.resx">
    <Generator>MSBuild:StrongTypeResourceInternal</Generator>
  </EmbeddedResource>
</ItemGroup>
```

For public access (recommended for WPF projects):
```xml
<ItemGroup>
  <EmbeddedResource Update="Resources\Text.resx">
    <Generator>MSBuild:StrongTypeResourcePublic</Generator>
  </EmbeddedResource>
</ItemGroup>
```

### 3. Remove the Original Generated File
In Solution Explorer, right-click on the generated file (`<YourResourceFile>.Designer.cs` next to your .resx file) and select **Delete**.
This prevents conflicts with the new StrongTypeResource-generated code.

## How Resources Are Generated

### Simple Strings ? Properties
Plain strings without formatting become `string` properties:
```
Welcome = Welcome to our application
```
Generates: `string Welcome { get; }`

### Formatted Strings ? Methods
Strings with placeholders become methods with parameters.
You must specify parameter types in the comment field of the main (neutral language) .resx file - comments in satellite .resx files are ignored.

**Resource Value:**
```
Found {0} items in {1} seconds.
```

**Comment:**
```csharp
{int itemCount, double seconds}
```

**Generated Method:**
```csharp
string FoundItems(int itemCount, double seconds)
```

### Skip Method Generation
To generate a formatted string as a property instead of a method, add a minus (`-`) at the beginning of the comment:
```
-This will be a property, not a method. Format items will not be validated.
```

### Enumeration Strings
For strings that should be restricted to specific values, add a comment with `!` followed by allowed values:
```
!(Value1, Value2, Value3)
```
This ensures the string in the main resource file or any satellite files matches one of the allowed values,
generating a compile-time error if it doesn't match.

## Special Features

### WPF Support
In WPF projects, the tool automatically generates a `FlowDirection` helper property for XAML binding.

### Pseudo Resources (Testing)
Generates longer, non-Latin character strings for UI testing while keeping them readable. This helps you test how your UI handles:
- **Longer text**: Pseudo strings are typically 30-50% longer than original text
- **Different character sets**: Uses accented and non-Latin characters to simulate international content
- **Layout issues**: Helps identify truncation, wrapping, and spacing problems before deploying to different cultures

For example, `"Save"` might become `"[-~=  v =~-]"` - longer and using accented characters, but still readable for testing.

**Enable pseudo resources:**

#### Option A: Using Visual Studio
In your project properties, enter `Pseudo` in the **Conditional compilation symbols** field.

#### Option B: Edit .csproj Directly
```xml
<PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|AnyCPU'">
  <DefineConstants>$(DefineConstants);Pseudo</DefineConstants>
</PropertyGroup>
```

### Legacy Compatibility
For transitioning from old resource generators to StrongTypeResource,
enable optional parameters to generate warnings instead of errors for formatted strings without proper parameter comments:

```xml
<PropertyGroup>
  <StrongTypeResourceOptionalParameters>true</StrongTypeResourceOptionalParameters>
</PropertyGroup>
```

This allows you to gradually migrate your resources - formatted strings without parameter comments will still generate properties
(like traditional resources) but with build warnings reminding you to add parameter definitions to get full strongly typed benefits.

## Automatic Verification

StrongTypeResource automatically verifies that:
- Format strings are valid for the specified parameter types
- Format parameters match between main and satellite .resx files
- All cultures have consistent parameter types and counts
- Potential runtime errors are caught at build time

Verification results appear in Visual Studio's Output window during build.
