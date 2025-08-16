using System.Diagnostics;
using System.Reflection;
using System.Resources;
using System.Text;
using Microsoft.Build.Utilities;
using StrongTypeResource;

namespace StrongTypeResourceUnitTests {
	[TestClass]
	public class GeneratorTest {
		public TestContext? TestContext { get; set; }

		private record ResouceString(string Name, string Value, string? Comment = null);

		private IEnumerable<ResouceString> GenerateResourceStrings(int count) {
			int seed = Environment.TickCount;
			this.TestContext?.WriteLine($"Seed for random generation: {seed}");
			Random random = new Random(seed);
			for(int i = 0; i < count; i++) {
				string name = $"String{i}";
				bool function = random.Next(0, 4) == 0;
				string value = $"Value {i}";
				string? comment = null;
				if(function) {
					value += " {0}.";
					comment = "{int i}";
				}
				yield return new ResouceString(name, value, comment);
			}
		}

		private IEnumerable<string> GenerateResxFiles(string folder, string name, int count) {
			List<ResouceString> strings = this.GenerateResourceStrings(count).ToList();
			string[] cultures = new[] { "ru", "de", "fr", "ar", "el", "es", "fa", "he", "hr", "hu", "it", "jp", "ko", "nt", "nl", "pl" };
			string writeResxFile(string fileName, string? culture) {
				if(string.IsNullOrEmpty(culture)) {
					fileName = Path.Combine(folder, $"{name}.resx");
				} else {
					fileName = Path.Combine(folder, $"{name}.{culture}.resx");
				}

				using StreamWriter streamWriter = new(fileName, false, Encoding.UTF8);
				ResXResourceWriter writer = new ResXResourceWriter(streamWriter);
				foreach(ResouceString resource in strings) {
					ResXDataNode node = new ResXDataNode(resource.Name, resource.Value);
					if(!string.IsNullOrWhiteSpace(resource.Comment)) {
						node.Comment = resource.Comment;
					}
					writer.AddResource(node);
				}
				writer.Generate();
				return fileName;
			}

			yield return writeResxFile(name, null);
			foreach(string culture in cultures) {
				yield return writeResxFile(name, culture);
			}
		}

		private StrongTypeResourceGenerator CreateGenerator(string resxPath) {
			string resxFile = Path.Combine(this.TestContext!.DeploymentDirectory!, resxPath!);
			Assert.IsTrue(File.Exists(resxFile), "Resx file does not exist: " + resxFile);
			TaskItem main = new TaskItem(resxFile);
			main.SetMetadata("Generator", "MSBuild:StrongTypeResourcePublic");
			TaskItem[] taskItems = [main];

			StrongTypeResourceGenerator generator = new() {
				ProjectDirectory = this.TestContext!.DeploymentDirectory!,
				ResxFiles = taskItems,
				CodeOutputPath = this.TestContext!.TestRunDirectory!,
				RootNamespace = "StrongTypeResourceUnitTests",
				NullableEnabled = true,
				PseudoCulture = false,
				FlowDirection = false,
				OptionalParameters = false,
				LogToConsole = true
			};

			return generator;
		}

		public string InterceptConsoleError(Action action) {
			TextWriter error = Console.Error;
			StringWriter stringWriter = new StringWriter();
			TextWriter textWriter = TextWriter.CreateBroadcasting(error, stringWriter);
			try {
				Console.SetError(textWriter);
				action();
			} finally {
				Console.SetError(error);
			}
			return stringWriter.ToString();
		}

		[TestMethod]
		public void PerformanceTest() {
			string rootFolder = Path.Combine(this.TestContext!.TestRunDirectory!, this.TestContext!.TestName!);
			Directory.CreateDirectory(rootFolder);
			TaskItem[] taskItems = this.GenerateResxFiles(rootFolder, "Strings", 10_000)
				.Select(file => new TaskItem(file))
				.ToArray()
			;
			TaskItem? main = taskItems.FirstOrDefault(i => i.ItemSpec.EndsWith(".resx", StringComparison.OrdinalIgnoreCase));
			Assert.IsNotNull(main);
			main.SetMetadata("Generator", "MSBuild:StrongTypeResourcePublic");

			StrongTypeResourceGenerator generator = new() {
				ProjectDirectory = rootFolder,
				ResxFiles = taskItems,
				CodeOutputPath = ".",
				RootNamespace = "StrongTypeResourceUnitTests",
				NullableEnabled = true,
				PseudoCulture = false,
				FlowDirection = false,
				OptionalParameters = false
			};

			generator.LogToConsole = true;
			Stopwatch stopwatch = Stopwatch.StartNew();
			bool result = generator.Execute();
			stopwatch.Stop();
			Assert.IsTrue(result, "Generator execution failed");
			this.TestContext.WriteLine($"Generator execution took {stopwatch.ElapsedMilliseconds} ms");
		}

		[TestMethod]
		public void OldVsNewGenerationTest() {
			Type oldType = typeof(StrongTypeResourceTest.OldResx.Resources);
			Type newType = typeof(StrongTypeResourceTest.NewResx.Resources);

			PropertyInfo[] oldProperties = oldType.GetProperties(BindingFlags.Public | BindingFlags.Static);
			PropertyInfo[] newProperties = newType.GetProperties(BindingFlags.Public | BindingFlags.Static);
			// new generator also provides formatting culture, so we expect one more property
			Assert.AreEqual(oldProperties.Length, newProperties.Length - 1, "Number of properties in old and new types do not match.");

			foreach (PropertyInfo oldProperty in oldProperties) {
				Console.WriteLine($"Checking property: {oldProperty.Name}");

				// Check if the property exists in the new type
				PropertyInfo? newProperty = newType.GetProperty(oldProperty.Name);
				Assert.IsNotNull(newProperty, $"Property '{oldProperty.Name}' not found in new type.");

				// Compare the types of the properties
				Assert.IsTrue(
					// Allow for UnmanagedMemoryStream to be compared with MemoryStream
					oldProperty.PropertyType == newProperty.PropertyType || oldProperty.PropertyType == typeof(UnmanagedMemoryStream) && newProperty.PropertyType == typeof(MemoryStream),
					$"Property '{oldProperty.Name}' has different types in old and new types."
				);
			}
		}

		[TestMethod]
		public void ParserDebugTest() {
			string resxFile = Path.Combine(this.TestContext!.TestRunDirectory!, @"..\..\..\StrongTypeResourceTest\NewResx\Resources.resx");
			Assert.IsTrue(File.Exists(resxFile), "Resx file does not exist: " + resxFile);
			List<ResourceItem> list = ResourceParser.Parse(resxFile, true, Enumerable.Empty<string>(), (f, s) => this.TestContext!.WriteLine(s), (f, s) => this.TestContext!.WriteLine(s)).ToList();
			Assert.AreEqual(7, list.Count, "Unexpected number of resources parsed from resx file.");
		}

		[TestMethod]
		[DeploymentItem(@"Resources\BadRoot.resx")]
		public void BadRootTest() {
			string errors = this.InterceptConsoleError(() => {
				StrongTypeResourceGenerator generator = this.CreateGenerator(@"BadRoot.resx");
				bool result = generator.Execute();
				Assert.IsFalse(result, "Generator should fail on bad root element in resx file.");
			});
			StringAssert.Contains(errors, "error : 'BadRoot' Root element is not <root>");
		}

		[TestMethod]
		[DeploymentItem(@"Resources\NoName.resx")]
		public void NoNameTest() {
			string errors = this.InterceptConsoleError(() => {
				StrongTypeResourceGenerator generator = this.CreateGenerator(@"NoName.resx");
				bool result = generator.Execute();
				Assert.IsFalse(result, "Generator should fail on bad root element in resx file.");
			});
			StringAssert.Contains(errors, "error : 'data' Resource name is missing");
		}

		[TestMethod]
		[DeploymentItem(@"Resources\EmptyData.resx")]
		public void EmptyDataTest() {
			string errors = this.InterceptConsoleError(() => {
				StrongTypeResourceGenerator generator = this.CreateGenerator(@"EmptyData.resx");
				bool result = generator.Execute();
				Assert.IsFalse(result, "Generator should fail on bad root element in resx file.");
			});
			StringAssert.Contains(errors, "error : 'StringText' resource value is missing");
		}

		[TestMethod]
		[DeploymentItem(@"Resources\ValueDuplicated.resx")]
		public void ValueDuplicatedTest() {
			string errors = this.InterceptConsoleError(() => {
				StrongTypeResourceGenerator generator = this.CreateGenerator(@"ValueDuplicated.resx");
				bool result = generator.Execute();
				Assert.IsFalse(result, "Generator should fail on bad root element in resx file.");
			});
			StringAssert.Contains(errors, "error : 'StringDup' resource value is duplicated: Hello, StringDup 1");
		}

		[TestMethod]
		[DeploymentItem(@"Resources\CommentDuplicated.resx")]
		public void CommentDuplicatedTest() {
			string errors = this.InterceptConsoleError(() => {
				StrongTypeResourceGenerator generator = this.CreateGenerator(@"CommentDuplicated.resx");
				bool result = generator.Execute();
				Assert.IsFalse(result, "Generator should fail on bad root element in resx file.");
			});
			StringAssert.Contains(errors, "error : 'CommentDup' resource comment is duplicated: Comment 1");
		}

		[TestMethod]
		[DeploymentItem(@"Resources\UnexpectedNode.resx")]
		public void UnexpectedNodeTest() {
			string errors = this.InterceptConsoleError(() => {
				StrongTypeResourceGenerator generator = this.CreateGenerator(@"UnexpectedNode.resx");
				bool result = generator.Execute();
				Assert.IsTrue(result, "Generator should fail on bad root element in resx file.");
			});

			StringAssert.Contains(errors, "warning : 'UnexpectedNode' unexpected node: hello");
			StringAssert.Contains(errors, "warning : 'UnexpectedNode' unexpected node: world");
		}

		[TestMethod]
		[DeploymentItem(@"Resources\NameDuplicated.resx")]
		public void NameDuplicatedTest() {
			string errors = this.InterceptConsoleError(() => {
				StrongTypeResourceGenerator generator = this.CreateGenerator(@"NameDuplicated.resx");
				bool result = generator.Execute();
				Assert.IsFalse(result, "Generator should fail on bad root element in resx file.");
			});
			StringAssert.Contains(errors, ".resx file is corrupted: 'name' is a duplicate attribute name");
		}

		[TestMethod]
		[DeploymentItem(@"Resources\TypeDuplicated.resx")]
		public void TypeDuplicated() {
			string errors = this.InterceptConsoleError(() => {
				StrongTypeResourceGenerator generator = this.CreateGenerator(@"TypeDuplicated.resx");
				bool result = generator.Execute();
				Assert.IsFalse(result, "Generator should fail on bad root element in resx file.");
			});
			StringAssert.Contains(errors, ".resx file is corrupted: 'type' is a duplicate attribute name");
		}

		[TestMethod]
		[DeploymentItem(@"Resources\Texts.resx", "Resources")]
		public void GeneratesCodeLocationTest() {
			string projectDirectory = this.TestContext!.DeploymentDirectory!;
			string resxFile = @"Resources\Texts.resx";
			Assert.IsTrue(File.Exists(Path.Combine(projectDirectory, resxFile)), "Resx file does not exist: " + resxFile);
			TaskItem main = new TaskItem(resxFile);
			main.SetMetadata("Generator", "MSBuild:StrongTypeResourcePublic");
			TaskItem[] taskItems = [main];

			StrongTypeResourceGenerator generator = new() {
				ProjectDirectory = projectDirectory,
				ResxFiles = taskItems,
				CodeOutputPath = this.TestContext!.TestRunDirectory!,
				RootNamespace = "StrongTypeResourceUnitTests",
				NullableEnabled = true,
				PseudoCulture = false,
				FlowDirection = false,
				OptionalParameters = false,
				LogToConsole = true
			};

			bool result = generator.Execute();
			Assert.IsTrue(result, "Generator execution failed");
			string codePath = Path.Combine(this.TestContext!.TestRunDirectory!, resxFile + ".cs");
			Assert.IsTrue(File.Exists(codePath), "Generated code file does not exist.");
			string code = File.ReadAllText(codePath);
			StringAssert.Contains(code, "get { return ResourceManager.GetString(\"String1\", Culture)!; }", "Generated code does not contain expected class declaration");
		}

		[TestMethod]
		[DeploymentItem(@"Resources\SpacePreserve.resx")]
		public void SpacePreserveTest() {
			StrongTypeResourceGenerator generator = this.CreateGenerator(@"SpacePreserve.resx");
			bool result = generator.Execute();
			Assert.IsTrue(result, "Generator execution failed");
			string codePath = Path.Combine(this.TestContext!.DeploymentDirectory!, "SpacePreserve.resx.cs");
			Assert.IsTrue(File.Exists(codePath), "Generated code file does not exist.");
			string code = File.ReadAllText(codePath);
			StringAssert.Contains(code, "get { return ResourceManager.GetString(\"String1\", Culture)!; }", "Generated code does not contain expected class declaration");
			StringAssert.Contains(code, "get { return ResourceManager.GetString(\"String2\", Culture)!; }", "Generated code does not contain expected class declaration");
			StringAssert.Contains(code, "get { return ResourceManager.GetString(\"String3\", Culture)!; }", "Generated code does not contain expected class declaration");
		}
	}
}
