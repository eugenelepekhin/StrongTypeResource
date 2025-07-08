# StrongTypeResource
StrongTypeResource package allows to access to .net resources via strongly typed methods and properties and does extra verification of satellite .resx files.
## Usage
To use the StrongTypeResource, you need to install the nuget package. Add resource files to your project.
To kick off the generation of strongly typed resources, you need to replace standard Custom Tool for your .resx files with `StrongTypeResource.internal` or `StrongTypeResource.public`.
You can do this in Visual Studio by right-clicking on the .resx file, selecting Properties, and changing the Custom Tool property.
Alternatively, you can edit your .csproj file directly. Here is an example of how to do this:
```xml
	<ItemGroup>
		<EmbeddedResource Update="Resources\Text.resx">
			<Generator>StrongTypeResource.internal</Generator>
		</EmbeddedResource>
	</ItemGroup>
```
You can also specify the `StrongTypeResource.public` generator if you want to make the generated class public.
This is useful if you want to access the resources from other projects or assemblies. Also, in WPF to be available in binding expressions.
```xml
	<ItemGroup>
		<EmbeddedResource Update="Resources\Text.resx">
			<Generator>StrongTypeResource.public</Generator>
		</EmbeddedResource>
	</ItemGroup
```
In the .resx file if you define a string without any formating, it will be generated as a property of type `string`.
If you define a string with formatting, it will be generated as a method that takes parameters.
For the parameters to be generated correctly, you need to add in comment list of parameters in curly braces `{}`.
For example, if you have a resource string named FoundItems:
```
Found {0} items in {1} seconds.
```
You need to add a comment like this in the .resx file:
```csharp
{int itemCount, double seconds}
```
This will generate a method `FoundItems(int itemCount, double seconds)` in the generated class.

If you want to deal with formatting yourself, you can cancel generation of the method by adding - (minus character) in the first position of the comment.

## Pseudo resources
It is usefull for testing purposes to generate pseudo resources that return longer and not Latin characters strings.
You will be able to read and understand pseudo string, so you can interact with UI.
To enable pseudo resources, you need to define Conditional symbol Pseudo in the project.

## Transition from old resources
To help with transition from old resources to StrongTypeResource, you can declare the `StrongTypeResourceOptionalParameters` property in your .csproj file.
This will allow you to use the old resource format with optional parameters.
```xml
	<PropertyGroup>
		<StrongTypeResourceOptionalParameters>true</StrongTypeResourceOptionalParameters>
	</PropertyGroup>
```

## Verification of satellite .resx files
StrongTypeResource performs verification of satellite .resx files to ensure that format parameter match the main .resx file.
This is done to prevent runtime errors when using resources in different cultures.
The verification is done during the build process and will report any discrepancies in the Output window of Visual Studio.
