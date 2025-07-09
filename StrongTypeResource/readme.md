# StrongTypeResource

StrongTypeResource is a NuGet package that provides strongly typed access to .NET resources with additional verification of satellite .resx files.

## What It Does

- **Strongly Typed Access**: Generate strongly typed classes that provide safe access to your .resx resources
- **Parameter Validation**: Automatically verify that format parameters match across different culture files
- **Build-Time Safety**: Catch resource-related errors during compilation instead of runtime

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
- `StrongTypeResource.internal` - Creates internal class
- `StrongTypeResource.public` - Creates public class (useful for cross-project access and WPF binding)

#### Option A: Using Visual Studio
1. Right-click your .resx file
2. Select **Properties**
3. Change **Custom Tool** to `StrongTypeResource.internal` or `StrongTypeResource.public`

#### Option B: Edit .csproj Directly
```xml
<ItemGroup>
    <EmbeddedResource Update="Resources\Text.resx">
        <Generator>StrongTypeResource.internal</Generator>
    </EmbeddedResource>
</ItemGroup>
```

For public access (recommended for WPF projects):
```xml
<ItemGroup>
    <EmbeddedResource Update="Resources\Text.resx">
        <Generator>StrongTypeResource.public</Generator>
    </EmbeddedResource>
</ItemGroup>
```

## How Resources Are Generated

### Simple Strings → Properties
Plain strings without formatting become `string` properties:
```
Welcome=Welcome to our application
```
Generates: `string Welcome { get; }`

### Formatted Strings → Methods
Strings with placeholders become methods with parameters. You must specify parameter types in the comment field:

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
-This will be a property, not a method
```

## Special Features

### WPF Support
In WPF projects, the tool automatically generates a `FlowDirection` helper property for XAML binding.

### Pseudo Resources (Testing)
Generate longer, non-Latin character strings for UI testing while keeping them readable. This helps you test how your UI handles:
- **Longer text**: Pseudo strings are typically 30-50% longer than original text
- **Different character sets**: Uses accented and non-Latin characters to simulate international content
- **Layout issues**: Helps identify truncation, wrapping, and spacing problems before deploying to different cultures

For example, `"Save"` might become `"[Šàvë!!!!]"` - longer and using accented characters, but still readable for testing.

**Enable pseudo resources:**
```xml
<PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|AnyCPU'">
  <DefineConstants>$(DefineConstants);Pseudo</DefineConstants>
</PropertyGroup>
```

### Legacy Compatibility
For transitioning from old resource systems to StrongTypeResource, enable optional parameters to generate warnings instead of errors for formatted strings without proper parameter comments:

```xml
<PropertyGroup>
    <StrongTypeResourceOptionalParameters>true</StrongTypeResourceOptionalParameters>
</PropertyGroup>
```

This allows you to gradually migrate your resources - formatted strings without parameter comments will still generate properties (like traditional resources) but with build warnings reminding you to add parameter definitions to get full strongly typed benefits.

## Automatic Verification

StrongTypeResource automatically verifies that:
- Format parameters match between main and satellite .resx files
- All cultures have consistent parameter types and counts
- Potential runtime errors are caught at build time

Verification results appear in Visual Studio's Output window during build.