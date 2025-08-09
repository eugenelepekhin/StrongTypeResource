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


		[TestMethod]
		public void PerformanceTest() {
			string rootFolder = Path.Combine(this.TestContext!.TestRunDirectory!, this.TestContext!.TestName!);
			Directory.CreateDirectory(rootFolder);
			TaskItem[] taskItems = this.GenerateResxFiles(rootFolder, "Strings", 1000)
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
	}
}
