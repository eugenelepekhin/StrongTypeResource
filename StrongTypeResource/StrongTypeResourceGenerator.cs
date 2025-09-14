// Ignore Spelling: Nullable Resx

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace StrongTypeResource {
	/// <summary>
	/// Generates strongly-typed resource wrappers for .resx files in a project.
	/// </summary>
	/// <remarks>This task processes .resx files specified in the <see cref="ResxFiles"/> property and generates C#
	/// code files containing strongly-typed resource wrappers. The generated files are placed in the directory specified
	/// by <see cref="CodeOutputPath"/>. The task supports customization of namespaces and class visibility based
	/// on metadata in the .resx and project files.</remarks>
	public class StrongTypeResourceGenerator : Task {
		/// <summary>
		/// Input parameter: the root directory of the project containing the .resx files.
		/// Usually this is the directory of the .csproj file and in the build process it is available via $(MSBuildProjectDirectory)
		/// </summary>
		[Required]
		public string? ProjectDirectory { get; set; }

		/// <summary>
		/// Input parameter: the list of .resx files to process.
		/// Usually these files are located in the project directory or its subdirectories.
		/// The list is available via the @(EmbeddedResource) items group.
		/// </summary>
		[Required]
		[SuppressMessage("Performance", "CA1819:Properties should not return arrays")]
		public ITaskItem[]? ResxFiles { get; set; }

		/// <summary>
		/// The root directory for the generated C# code files relative to ProjectDirectory.
		/// This is usually available via the $(IntermediateOutputPath) property in the build process.
		/// </summary>
		[Required]
		public string? CodeOutputPath { get; set; }

		/// <summary>
		/// The root namespace of the project.
		/// This string is available via the $(RootNamespace) property in the build process.
		/// </summary>
		[Required]
		public string? RootNamespace { get; set; }

		/// <summary>
		/// True if project is set to nullable check via <Nullable>enable</Nullable> property in .csproj file.
		/// </summary>
		public bool NullableEnabled { get; set; }

		/// <summary>
		/// True if the generated wrappers should include pseudo-localization for testing purposes.
		/// </summary>
		public bool PseudoCulture { get; set; }

		/// <summary>
		/// True if the generated wrappers should include flow direction property of current culture.
		/// This property is set to true for WPF projects to support right-to-left languages.
		/// </summary>
		public bool FlowDirection { get; set; }

		/// <summary>
		/// True if the generated wrappers should allow missing parameters in resource values.
		/// This is useful for legacy resources that in transition to strongly-typed resources for the period of conversion.
		/// To set this property to true define <StrongTypeResourceOptionalParameters>True</StrongTypeResourceOptionalParameters> in the .csproj file.
		/// </summary>
		public bool OptionalParameters { get; set; }

		/// <summary>
		/// Output parameter: the list of generated resource wrapper files.
		/// </summary>
		[Output]
		[SuppressMessage("Performance", "CA1819:Properties should not return arrays")]
		public ITaskItem[]? ResourceWrapperFiles { get; set; }

		/// <summary>
		/// This is used in unit tests to log messages to console instead of MSBuild log.
		/// </summary>
		internal bool LogToConsole { get; set; }

		private sealed class ResourceGroup {
			public string ItemSpec { get; }
			public string ResxPath { get; }
			public string CodePath { get; }
			public string Name { get; }
			public string Namespace { get; }
			public string ClassName { get; }
			public bool IsPublic { get; }

			private List<string> satellites = new List<string>();
			public IEnumerable<string> Satellites => this.satellites;

			public ResourceGroup(string itemSpec, string resxPath, string codePath, string name, string nameSpace, string className, bool isPublic) {
				this.ItemSpec = itemSpec;
				this.ResxPath = resxPath;
				this.CodePath = codePath;
				this.Name = name;
				this.Namespace = nameSpace;
				this.ClassName = className;
				this.IsPublic = isPublic;
			}

			public void AddSatellite(string path) {
				this.satellites.Add(path);
			}

			public bool IsMainResource(string path) => StringComparer.OrdinalIgnoreCase.Equals(this.ResxPath, path);

			#if DEBUG
				public override string ToString() {
					return
						$"ItemSpec: {this.ItemSpec},\n" +
						$"ResxPath: {this.ResxPath},\n" +
						$"CodePath: {this.CodePath},\n" +
						$"Name: {this.Name},\n" +
						$"Namespace: {this.Namespace},\n" +
						$"ClassName: {this.ClassName},\n" +
						$"IsPublic: {this.IsPublic},\n" +
						$"Satellites: [{string.Join(", ", this.Satellites)}]"
					;
				}
			#endif
		}

		[SuppressMessage("Design", "CA1031:Do not catch general exception types")]
		public override bool Execute() {
			this.LogMessage("StrongTypeResourceGenerator started");
			if(string.IsNullOrWhiteSpace(this.ProjectDirectory)) {
				this.LogError(null, "StrongTypeResourceGenerator: ProjectDirectory is not set.");
				return false;
			}
			if(string.IsNullOrWhiteSpace(this.CodeOutputPath)) {
				this.LogError(null, "StrongTypeResourceGenerator: CodeOutputPath is not set.");
				return false;
			}
			if(string.IsNullOrWhiteSpace(this.RootNamespace)) {
				this.LogError(null, "StrongTypeResourceGenerator: RootNamespace is not set.");
				return false;
			}
			if(this.ResxFiles != null && 0 < this.ResxFiles.Length) {
				this.ProjectDirectory = this.ProjectDirectory!.Trim();
				this.CodeOutputPath = this.CodeOutputPath!.Trim();
				this.RootNamespace = this.RootNamespace!.Trim();

				try {
					return this.Generate();
				} catch(XmlException xmlException) {
					this.LogError(xmlException.SourceUri, $".resx file is corrupted: {xmlException.Message}");
				} catch(IOException ioException) {
					this.LogError(null, $"IO error occurred while processing .resx files: {ioException.Message}");
				} catch(Exception exception) {
					this.LogError(null, $"Unexpected error occurred: {exception.Message}");
				}
				return false;
			}
			return true;
		}

		private void LogError(string? file, string message) {
			if (this.LogToConsole) {
				Console.Error.WriteLine($"{file ?? string.Empty}: error : {message}");
			} else {
				this.Log.LogError(null, null, null, file, 0, 0, 0, 0, message);
			}
		}

		private void LogWarning(string? file, string message) {
			if (this.LogToConsole) {
				Console.Error.WriteLine($"{file ?? string.Empty}: warning : {message}");
			} else {
				this.Log.LogWarning(null, null, null, file, 0, 0, 0, 0, message);
			}
		}

		private void LogMessage(string message) {
			if (this.LogToConsole) {
				Console.WriteLine(message);
			} else {
				this.Log.LogMessage(MessageImportance.High, message);
			}
		}

		private List<ResourceGroup> BuildGroups(Action<string?, string> errorMessage, Action<string?, string> warningMessage) {
			// Remove all duplicate resx files by their ItemSpec (path)
			Dictionary<string, ITaskItem> uniqueResxFiles = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);
			foreach(ITaskItem item in this.ResxFiles!) {
				if(Path.GetExtension(item.ItemSpec).Equals(".resx", StringComparison.OrdinalIgnoreCase)) {
					string key = item.ItemSpec.Trim();
					if(!uniqueResxFiles.ContainsKey(key)) {
						uniqueResxFiles[key] = item;
					}
				}
			}

			List<ResourceGroup> groups = new List<ResourceGroup>();
			List<ITaskItem> others = new List<ITaskItem>();
			// collect all main resx files with defined generator and store all other resx files in others list
			foreach(ITaskItem item in uniqueResxFiles.Values) {
				string generator = item.GetMetadata("Generator");
				bool generatorIs(string value) => string.Equals(generator, value, StringComparison.OrdinalIgnoreCase);
				bool isPublic = generatorIs("MSBuild:StrongTypeResourcePublic") || generatorIs("StrongTypeResource.public");
				bool isInternal = generatorIs("MSBuild:StrongTypeResourceInternal") || generatorIs("StrongTypeResource.internal");
				if(isPublic || isInternal) {
					string resourcePath = item.ItemSpec.Trim();
					string linkPath = item.GetMetadata("Link").Trim();
					if(string.IsNullOrEmpty(linkPath)) {
						linkPath = resourcePath;
						while(linkPath.StartsWith(@"..\", StringComparison.Ordinal)) {
							linkPath = linkPath.Substring(3);
						}
					}
					string resourceRoot = Path.GetDirectoryName(linkPath) ?? string.Empty;
					string resourceFile = Path.GetFileNameWithoutExtension(linkPath);
					if(Path.HasExtension(resourceFile)) {
						warningMessage(Path.Combine(this.ProjectDirectory, linkPath),
							string.Format(
								CultureInfo.InvariantCulture,
								"StrongTypeResourceGenerator: The main resource file '{0}' has culture extension in its name. The main resource files should not have any culture extensions and be the assembly neutral culture.",
								Path.GetFileName(linkPath)
							)
						);
					}
					string resourceName = !string.IsNullOrEmpty(resourceRoot)
						? string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", this.RootNamespace, resourceRoot.Replace('\\', '.') , resourceFile)
						: string.Format(CultureInfo.InvariantCulture, "{0}.{1}", this.RootNamespace, resourceFile)
					;
					string nameSpace = item.GetMetadata("CustomToolNamespace").Trim();
					if(string.IsNullOrWhiteSpace(nameSpace)) {
						nameSpace =  !string.IsNullOrEmpty(resourceRoot)
							? string.Format(CultureInfo.InvariantCulture, "{0}.{1}", this.RootNamespace!, resourceRoot.Replace('\\', '.'))
							: this.RootNamespace!
						;
					}
					ResourceGroup group = new ResourceGroup(
						itemSpec: linkPath,
						resxPath: Path.Combine(this.ProjectDirectory,  resourcePath),
						codePath: Path.Combine(this.CodeOutputPath, linkPath + ".cs"),
						name: resourceName,
						nameSpace: nameSpace,
						className: resourceFile.Replace('.', '_'),
						isPublic: isPublic
					);
					groups.Add(group);
				} else {
					others.Add(item);
				}
			}
			// now group all satellite resx files with main resx files. Note that usually it's going to be just one group.
			foreach(ResourceGroup group in groups) {
				string groupPath = Path.ChangeExtension(Path.ChangeExtension(group.ItemSpec, null), null); // remove .resx extension and if culture extension exists remove it too.
				Regex regex = new Regex(Regex.Escape(groupPath) + @"\.[a-zA-Z\-01]{2,20}\.resx", RegexOptions.Compiled | RegexOptions.IgnoreCase);
				foreach(ITaskItem satellite in others) {
					string path = satellite.ItemSpec;
					if(regex.IsMatch(path) && !group.IsMainResource(path)) {
						group.AddSatellite(Path.Combine(this.ProjectDirectory,  path));
					}
				}
			}
			return groups;
		}

		private bool Generate() {
			int errorCount = 0;
			int warningCount = 0;
			void error(string? file, string message) { errorCount++; this.LogError(file, message); }
			void warning(string? file, string message) { warningCount++; this.LogWarning(file, message); }

			List<ResourceGroup> groups = this.BuildGroups(error, warning);
			foreach(ResourceGroup group in groups) {
				#if DEBUG
					this.LogMessage($"Processing resource group:\n{group}");
				#endif
				IEnumerable<ResourceItem> items = ResourceParser.Parse(
					group.ResxPath,
					!this.OptionalParameters,
					group.Satellites,
					error,
					warning
				);
				if(errorCount == 0) {
					WrapperGenerator wrapper = new WrapperGenerator(group.Namespace, group.ClassName, group.Name, group.IsPublic, this.PseudoCulture, this.FlowDirection, this.NullableEnabled, items);
					wrapper.Generate(Path.Combine(this.ProjectDirectory, group.CodePath));
				}
			}
			if(errorCount == 0) {
				this.ResourceWrapperFiles = groups.Select(g => new TaskItem(g.CodePath, true)).ToArray();
			}
			this.LogMessage($"StrongTypeResourceGenerator completed with {errorCount} errors and {warningCount} warnings.");
			return errorCount == 0;
		}
	}
}
