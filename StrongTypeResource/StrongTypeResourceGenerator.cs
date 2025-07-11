// Ignore Spelling: Nullable Resx

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace StrongTypeResource {
	/// <summary>
	/// Generates strongly-typed resource wrappers for .resx files in a project.
	/// </summary>
	/// <remarks>This task processes .resx files specified in the <see cref="ResxFiles"/> property and generates C#
	/// code files containing strongly-typed resource wrappers. The generated files are placed in the directory specified
	/// by <see cref="CodeOutputPath"/>. The task supports customization of namespaces and class visibility based
	/// on metadata in the .resx files.</remarks>
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
		public ITaskItem[]? ResxFiles { get; set; } = null;

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
		public ITaskItem[]? ResourceWrapperFiles { get; set; } = null;

		private sealed class ResourceGroup {
			public string ItemSpec { get; }
			public string ResxPath { get; }
			public string CodePath { get; }
			public string Name { get; }
			public string Namespace { get; }
			public string ClassName { get; }
			public bool IsPublic { get; }

			private HashSet<string> satellites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
		}

		public override bool Execute() {
			this.LogMessage("StrongTypeResourceGenerator started");
			if(string.IsNullOrWhiteSpace(this.ProjectDirectory)) {
				this.LogError("StrongTypeResourceGenerator: ProjectDirectory is not set.");
				return false;
			}
			if(string.IsNullOrWhiteSpace(this.CodeOutputPath)) {
				this.LogError("StrongTypeResourceGenerator: CodeOutputPath is not set.");
				return false;
			}
			if(string.IsNullOrWhiteSpace(this.RootNamespace)) {
				this.LogError("StrongTypeResourceGenerator: RootNamespace is not set.");
				return false;
			}
			if (this.ResxFiles != null && 0 < this.ResxFiles.Length) {
				this.CodeOutputPath = this.CodeOutputPath!.Trim();
				this.RootNamespace = this.RootNamespace!.Trim();

				List<ResourceGroup> groups = this.BuildGroups();
				//this.LogInfo(groups);
				return this.Parse();
			}
			return true;
		}

		private void LogError(string message) {
			this.Log.LogError(message);
		}

		private void LogWarning(string message) {
			this.Log.LogWarning(message);
		}

		private void LogMessage(string message) {
			this.Log.LogMessage(MessageImportance.High, message);
		}

		private List<ResourceGroup> BuildGroups() {
			List<ResourceGroup> groups = new List<ResourceGroup>();
			List<ITaskItem> others = new List<ITaskItem>();
			// first collect all main resx files with defined generator and store all other resx files in other list
			foreach(ITaskItem item in this.ResxFiles!) {
				if(Path.GetExtension(item.ItemSpec).Equals(".resx", StringComparison.OrdinalIgnoreCase)) {
					string generator = item.GetMetadata("Generator");
					bool isPublic = generator == "StrongTypeResource.public";
					bool isInternal = generator == "StrongTypeResource.internal";
					if (isPublic || isInternal) {
						string resourcePath = item.ItemSpec;
						string resourceRoot = Path.GetDirectoryName(resourcePath) ?? string.Empty;
						string resourceFile = Path.GetFileNameWithoutExtension(resourcePath);
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
							itemSpec: resourcePath,
							resxPath: Path.Combine(this.ProjectDirectory,  resourcePath),
							codePath: Path.Combine(this.CodeOutputPath, resourceRoot, resourceFile + ".resx.cs"),
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
			}
			// now group all satellite resx files with main resx files
			foreach(ResourceGroup group in groups) {
				Regex regex = new Regex(Regex.Escape(Path.ChangeExtension(group.ItemSpec, null)) + @"\.[a-zA-Z\-]{2,20}\.resx", RegexOptions.Compiled | RegexOptions.IgnoreCase);
				foreach(ITaskItem satellite in others) {
					string path = satellite.ItemSpec;
					if(regex.IsMatch(path) && !group.IsMainResource(path)) {
						group.AddSatellite(Path.Combine(this.ProjectDirectory,  path));
					}
				}
			}
			return groups;
		}

		private bool Parse() {
			List<ResourceGroup> groups = this.BuildGroups();
			int errors = 0;
			int warnings = 0;
			foreach(ResourceGroup group in groups) {
				IEnumerable<ResourceItem> items = ResourceParser.Parse(
					group.ResxPath,
					!this.OptionalParameters,
					group.Satellites,
					message => { errors++; this.LogError(message); },
					message => { warnings++; this.LogWarning(message);}
				);
				if(errors == 0) {
					WrapperGenerator wrapper = new WrapperGenerator(group.Namespace, group.ClassName, group.Name, group.IsPublic, this.PseudoCulture, this.FlowDirection, this.NullableEnabled, items);
					wrapper.Generate(Path.Combine(this.ProjectDirectory, group.CodePath));
				}
			}
			if(errors == 0) {
				this.ResourceWrapperFiles = groups.Select(g => new TaskItem(g.CodePath, true)).ToArray();
			}
			this.LogMessage($"StrongTypeResourceGenerator completed with {errors} errors and {warnings} warnings.");
			return errors == 0;
		}
	}
}
