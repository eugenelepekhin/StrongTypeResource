// Ignore Spelling: Nullable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace StrongTypeResource {
	partial class WrapperGenerator {
		public string NameSpace { get; }
		public string ClassName { get; }
		public string ResourceName { get; }
		public bool IsPublic { get; }
		public bool Pseudo { get; }
		public bool FlowDirection { get; }
		public bool NullableEnabled { get; }
		public string AllowNull => this.NullableEnabled ? "?" : string.Empty;
		public string NotNull => this.NullableEnabled ? "!" : string.Empty;
		public IEnumerable<ResourceItem> Items { get; }

		public WrapperGenerator(
			string nameSpace,
			string className,
			string resourceName,
			bool isPublic,
			bool pseudo,
			bool flowDirection,
			bool nullableEnabled,
			IEnumerable<ResourceItem> items
		) {
			this.NameSpace = nameSpace;
			this.ClassName = className;
			this.ResourceName = resourceName;
			this.IsPublic = isPublic;
			this.Pseudo = pseudo;
			this.FlowDirection = flowDirection;
			this.NullableEnabled = nullableEnabled;
			this.Items = items;
		}

		public void Generate(string code) {
			string content = this.TransformText();
			string? oldFileContent = null;
			if(File.Exists(code)) {
				oldFileContent = File.ReadAllText(code, Encoding.UTF8);
			}
			if(!StringComparer.Ordinal.Equals(oldFileContent, content)) {
				string? directory = Path.GetDirectoryName(code);
				Debug.Assert(directory != null, "Directory name should not be null");
				if(!Directory.Exists(directory)) {
					Directory.CreateDirectory(directory!);
				}
				File.WriteAllText(code, content, Encoding.UTF8);
			}
		}
	}
}
