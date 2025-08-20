using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using StrongTypeResource;

namespace StrongTypeResourceUnitTests {
	[TestClass]
	public class ResourceParserTest {
		private record Res(string name, string value, string? comment);

		public TestContext TestContext { get; set; }

		/// <summary>
		/// Creates content of resource file based on provided resources. Each array should be 3 elements: name, value, comment.
		/// </summary>
		/// <param name="resources"></param>
		/// <returns></returns>
		private string CreateResxContent(IEnumerable<Res> resources) {
			StringBuilder text = new StringBuilder();
			using(StringWriter stringWriter = new StringWriter(text)) {
				ResXResourceWriter writer = new ResXResourceWriter(stringWriter);
				foreach(Res resource in resources) {
					ResXDataNode node = new ResXDataNode(resource.name, resource.value);
					if(!string.IsNullOrWhiteSpace(resource.comment)) {
						node.Comment = resource.comment;
					}
					writer.AddResource(node);
				}
				writer.Generate();
			}
			return text.ToString();
		}

		/// <summary>
		/// Generates name of temp file in test results directory
		/// </summary>
		/// <param name="ext"></param>
		/// <returns></returns>
		private string TempFile(string ext = ".resx") {
			return Path.Combine(this.TestContext.TestRunDirectory!, this.TestContext.TestName + DateTime.UtcNow.Ticks.ToString() + ext);
		}

		private string WriteFile(IEnumerable<Res> resources) {
			string path = this.TempFile();
			string text = this.CreateResxContent(resources);
			File.WriteAllText(path, text);
			return path;
		}

		private Res R(string name, string value, string? comment) => new Res(name, value, comment);
		private Res[] R(params Res[] args) => args;

		private void ThrowOnErrorMessage(string? file, string message) {
			this.TestContext.WriteLine($"Unexpected error message: {message}");
			throw new InvalidOperationException(message);
		}

		private void ExpectErrorMessage(string expecting, string? actual) {
			this.TestContext.WriteLine($"Expected error message: {expecting}");
			this.TestContext.WriteLine($"Actual error message: {actual ?? "NULL"}");
			Assert.IsTrue(!string.IsNullOrEmpty(actual) && Regex.IsMatch(actual, Regex.Escape(expecting), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline), "Unexpected error message.");
		}

		[TestMethod]
		public void NoParametersTest() {
			string path = this.WriteFile(R(
				R("a", "b", "c")
			));
			IEnumerable<ResourceItem> actual = ResourceParser.Parse(path, true, [], this.ThrowOnErrorMessage, this.ThrowOnErrorMessage);
			Assert.AreEqual(1, actual.Count());
			ResourceItem item = actual.First();
			Assert.IsNotNull(item);
			Assert.AreEqual("a", item.Name);
			Assert.AreEqual("b", item.Value);
			Assert.IsNull(item.LocalizationVariants);
			Assert.IsNull(item.Parameters);
			Assert.AreEqual("b", item.Comment());
			Assert.AreEqual("ResourceManager.GetString(", item.GetStringExpression(false));
			Assert.AreEqual("ResourceManager.GetString(", item.GetStringExpression(true));
		}

		[TestMethod]
		public void WithParametersTest() {
			string expected = "d{0}c{1}";
			string path = this.WriteFile(R(
				R("a", expected, "{int i, int j}")
			));
			IEnumerable<ResourceItem> actual = ResourceParser.Parse(path, true, [], this.ThrowOnErrorMessage, this.ThrowOnErrorMessage);
			Assert.AreEqual(1, actual.Count());
			ResourceItem item = actual.First();
			Assert.IsNotNull(item);
			Assert.AreEqual("a", item.Name);
			Assert.AreEqual(expected, item.Value);
			Assert.IsNull(item.LocalizationVariants);
			Assert.IsNotNull(item.Parameters);
			Assert.AreEqual(expected, item.Comment());
			Assert.AreEqual("ResourceManager.GetString(", item.GetStringExpression(false));
			Assert.AreEqual("ResourceManager.GetString(", item.GetStringExpression(true));
			Assert.AreEqual("i, j", item.ParametersInvocation());
			Assert.AreEqual("int i, int j", item.ParametersDeclaration());
		}

		[TestMethod]
		public void EmptyValueTest() {
			string expected = string.Empty;
			string path = this.WriteFile(R(
				R("a", expected, null)
			));
			IEnumerable<ResourceItem> actual = ResourceParser.Parse(path, true, [], this.ThrowOnErrorMessage, this.ThrowOnErrorMessage);
			Assert.AreEqual(1, actual.Count());
			ResourceItem item = actual.First();
			Assert.IsNotNull(item);
			Assert.AreEqual("a", item.Name);
			Assert.AreEqual(expected, item.Value);
			Assert.IsNull(item.LocalizationVariants);
			Assert.IsNull(item.Parameters);
			string comment = item.Comment();
			this.TestContext.WriteLine($"Comment: {comment}");
			Assert.AreEqual(0, comment.Length);
		}

		[TestMethod]
		public void MultiLineCommentTest() {
			string expected = "first line\nsecond line\nthird line\nforth line\nfifth line";
			string path = this.WriteFile(R(
				R("a", expected, null)
			));
			IEnumerable<ResourceItem> actual = ResourceParser.Parse(path, true, [], this.ThrowOnErrorMessage, this.ThrowOnErrorMessage);
			Assert.AreEqual(1, actual.Count());
			ResourceItem item = actual.First();
			Assert.IsNotNull(item);
			Assert.AreEqual("a", item.Name);
			Assert.AreEqual(expected, item.Value);
			Assert.IsNull(item.LocalizationVariants);
			Assert.IsNull(item.Parameters);
			string comment = item.Comment();
			this.TestContext.WriteLine($"Comment: {comment}");
			StringAssert.Matches(comment, new Regex(@"first line", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline));
			StringAssert.Matches(comment, new Regex(@"second line", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline));
			StringAssert.Matches(comment, new Regex(@"third line", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline));
			StringAssert.DoesNotMatch(comment, new Regex(@"forth line", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline));
			StringAssert.DoesNotMatch(comment, new Regex(@"fifth line", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline));
		}

		[TestMethod]
		public void WarningMissingParameterDeclarationTest() {
			string expected = "d{0}c{1}";
			string path = this.WriteFile(R(
				R("a", expected, null)
			));
			string? warning = null;
			IEnumerable<ResourceItem> actual = ResourceParser.Parse(path, false, [], this.ThrowOnErrorMessage, (f, message) => warning = message);
			this.ExpectErrorMessage("parameters declaration is missing in the comment", warning!);
			Assert.AreEqual(1, actual.Count());
			ResourceItem item = actual.First();
			Assert.IsNotNull(item);
			Assert.AreEqual("a", item.Name);
			Assert.AreEqual(expected, item.Value);
			Assert.IsNull(item.LocalizationVariants);
			Assert.IsNull(item.Parameters);
			Assert.AreEqual(expected, item.Comment());
			Assert.AreEqual("ResourceManager.GetString(", item.GetStringExpression(false));
			Assert.AreEqual("ResourceManager.GetString(", item.GetStringExpression(true));
		}

		private void AssertError(string name, string value, string? comment, string expectedError) {
			string path = this.WriteFile(R(
				R(name, value, comment)
			));
			List<string> errors = [];
			void addError(string message) {
				this.TestContext.WriteLine($"Error: {message}");
				errors.Add(message);
			}
			IEnumerable<ResourceItem> actual = ResourceParser.Parse(path, true, [], (f, message) => addError(message), this.ThrowOnErrorMessage);
			Assert.IsTrue(0 < errors.Count);
			this.ExpectErrorMessage(expectedError, errors[0]);
			Assert.IsFalse(actual.Any());
		}

		[TestMethod]
		public void ErrorMissingParameterDeclarationTest() {
			this.AssertError("a", "d{0}c{1}", null, "parameters declaration is missing in the comment");
		}

		[TestMethod]
		public void ErrorMissingFormatItemsTest() {
			this.AssertError("a", "d0c1", "{int count, double percent}", "no format items are used in the value, but function parameters are declared in the comment");
		}

		[TestMethod]
		public void MissingFormat0Test() {
			this.AssertError("a", "{2}{1}", "{int i, int j}", "'a' format placeholder '{0}' is not used in the format string");
		}

		[TestMethod]
		public void MissingFormat1Test() {
			this.AssertError("a", "{2}{0}", "{int i, int j}", "'a' format placeholder '{1}' is not used in the format string");
		}

		[TestMethod]
		public void WrongNumberOfParameters1Test() {
			this.AssertError("a", "{2}{0}{1}", "{int i, int j}", "the number of format placeholders in the string doesn't match number of parameters listed in the comment");
		}

		[TestMethod]
		public void WrongNumberOfParameters2Test() {
			this.AssertError("a", "{2}{0}{1}", "{int i, int j, int k, int l}", "the number of format placeholders in the string doesn't match number of parameters listed in the comment");
		}

		[TestMethod]
		public void DeclarationParameters1Test() {
			this.AssertError("a", "{0}", "{int_i}", "bad parameter declaration: int_i");
		}

		[TestMethod]
		public void DeclarationParameters2Test() {
			this.AssertError("a", "{0}{1}", "{int i int j}", "bad parameter declaration: int i int j");
		}

		[TestMethod]
		public void MultipleTest() {
			string path = this.WriteFile(R(
				R("a", "b", "c"),
				R("d", "e", "f"),
				R("g", "h", "i"),
				R("j", "k{0}l{1}", "{int i, int j}"),
				R("m", "n{0}o{1}", "{int i, int j}"),
				R("p", "q{0}r{1}", "{int i, int j}")
			));
			int errors = 0;
			int warnings = 0;
			IEnumerable<ResourceItem> actual = ResourceParser.Parse(path, true, [], (f, s) => errors++, (f, s) => warnings++);
			Assert.AreEqual(6, actual.Count());
			Assert.AreEqual(0, errors);
			Assert.AreEqual(0, warnings);
		}

		[TestMethod]
		public void Variants1Test() {
			string path = this.WriteFile(R(
				R("a", "d", "!(c, d, e)")
			));
			int errors = 0;
			int warnings = 0;
			IEnumerable<ResourceItem> actual = ResourceParser.Parse(path, true, [], (f, s) => errors++, (f, s) => warnings++);
			Assert.AreEqual(1, actual.Count());
			Assert.AreEqual(0, errors);
			Assert.AreEqual(0, warnings);
			ResourceItem item = actual.First();
			CollectionAssert.AreEqual(item.LocalizationVariants!.ToArray(), new string[] {"c", "d", "e"});
		}

		[TestMethod]
		public void Variants2Test() {
			string path = this.WriteFile(R(
				R("a", "{0}", "!(c, {0}, e)")
			));
			int errors = 0;
			int warnings = 0;
			IEnumerable<ResourceItem> actual = ResourceParser.Parse(path, true, [], (f, s) => errors++, (f, s) => warnings++);
			Assert.AreEqual(1, actual.Count());
			Assert.AreEqual(0, errors);
			Assert.AreEqual(0, warnings);
			ResourceItem item = actual.First();
			CollectionAssert.AreEqual(item.LocalizationVariants!.ToArray(), new string[] { "c", "{0}", "e" });
		}

		[TestMethod]
		public void IgnoreParameters1Test() {
			string path = this.WriteFile(R(
				R("a", "{0}", "-{int i}")
			));
			int errors = 0;
			int warnings = 0;
			IEnumerable<ResourceItem> actual = ResourceParser.Parse(path, true, [], (f, s) => errors++, (f, s) => warnings++);
			Assert.AreEqual(1, actual.Count());
			Assert.AreEqual(0, errors);
			Assert.AreEqual(0, warnings);
			ResourceItem item = actual.First();
			Assert.IsNull(item.Parameters);
			Assert.IsNull(item.LocalizationVariants);
		}

		[TestMethod]
		public void ErrorIgnoreParameters1Test() {
			string path = this.WriteFile(R(
				R("a", "{1}", "-{int i}")
			));
			int errors = 0;
			int warnings = 0;
			IEnumerable<ResourceItem> actual = ResourceParser.Parse(path, true, [], (f, s) => errors++, (f, s) => warnings++);
			Assert.AreEqual(1, actual.Count());
			Assert.AreEqual(0, errors);
			Assert.AreEqual(0, warnings);
			ResourceItem item = actual.First();
			Assert.IsNull(item.Parameters);
			Assert.IsNull(item.LocalizationVariants);
		}

		[TestMethod]
		public void ErrorIgnoreParameters2Test() {
			string path = this.WriteFile(R(
				R("a", "{0}{5}{1}", "-")
			));
			int errors = 0;
			int warnings = 0;
			IEnumerable<ResourceItem> actual = ResourceParser.Parse(path, true, [], (f, s) => errors++, (f, s) => warnings++);
			Assert.AreEqual(1, actual.Count());
			Assert.AreEqual(0, errors);
			Assert.AreEqual(0, warnings);
			ResourceItem item = actual.First();
			Assert.IsNull(item.Parameters);
			Assert.IsNull(item.LocalizationVariants);
		}

		[TestMethod]
		public void ErrorIgnoreParameters3Test() {
			string path = this.WriteFile(R(
				R("a", "{Hello}", "-")
			));
			int errors = 0;
			int warnings = 0;
			IEnumerable<ResourceItem> actual = ResourceParser.Parse(path, true, [], (f, s) => errors++, (f, s) => warnings++);
			Assert.AreEqual(1, actual.Count());
			Assert.AreEqual(0, errors);
			Assert.AreEqual(0, warnings);
			ResourceItem item = actual.First();
			Assert.IsNull(item.Parameters);
			Assert.IsNull(item.LocalizationVariants);
		}

		[TestMethod]
		public void VariantsError1Test() {
			this.AssertError("a", "b", "!(c, d, e)", @"provided value 'b' is not in the list of allowed options: (c, d, e)");
		}

		[TestMethod]
		public void ComplicatedFormatItemTest() {
			string path = this.WriteFile(R(
				R("a", "int {0,-5:d3}", "{int i}")
			));
			int errors = 0;
			int warnings = 0;
			IEnumerable<ResourceItem> actual = ResourceParser.Parse(path, true, [], (f, s) => errors++, (f, s) => warnings++);
			Assert.AreEqual(1, actual.Count());
			Assert.AreEqual(0, errors);
			Assert.AreEqual(0, warnings);
			ResourceItem item = actual.First();
			Assert.AreEqual(1, item.Parameters!.Count);
			Assert.IsNull(item.LocalizationVariants);
		}

		[TestMethod]
		public void FormatValidationTest() {
			int count = 0;
			void valid(string value, string? comment) {
				string path = this.WriteFile(R(R("a" + ++count, value, comment)));
				int errors = 0;
				int warnings = 0;
				IEnumerable<ResourceItem> actual = ResourceParser.Parse(path, true, [], (f, s) => errors++, (f, s) => warnings++);
				Assert.AreEqual(1, actual.Count());
				Assert.AreEqual(0, errors);
				Assert.AreEqual(0, warnings);
			}
			void error(string value, string message) {
				this.AssertError("a" + ++count, value, null, message);
			}
			string error1 = "invalid format item";
			string one = "{int i}";

			// test escaping curly
			error("}", error1);
			valid("}}", null);
			error("{", error1);
			valid("{{", null);
			valid("{{ {{{{ }} }}}}}} }}", null);

			//test validation of parameter number
			valid("{0}", one);
			valid("a{0}b", one);
			error("{ 0}", error1);
			error("{a}", error1);
			error("{1000000}", error1);

			//test validation of alignment
			valid("{0,5}", one);
			valid("{0  ,5}", one);
			valid("{0,-5}", one);
			valid("{0,   -5}", one);
			valid("{0,  59}", one);
			error("{0,b}", error1);
			error("{0,1000000}", error1);
			error("{0,}", error1);

			//test validation of format string
			valid("{0:x}", one);
			error("{0:}", error1);
			valid("{0  : C}", one);
			valid("{0, -10  : G}", one);
			error("{0: }}{{ }} x}", error1);
			error("{0: }}{{ }}   }", error1);
			error("{0: { }", error1);
			error("{0:}}", error1);

			//test validation of missing parameter numbers
			valid("{2}{0}{1}", "{int i, int j, int k}");
			error("{2}{0}", "string value contains formating placeholders, but the function parameters declaration is missing in the comment");
		}

		[TestMethod]
		public void EscapeCurlyTest() {
			void valid(string path, int parameterCount) {
				int errors = 0;
				int warnings = 0;
				IEnumerable<ResourceItem> actual = ResourceParser.Parse(path, true, [], (f, s) => errors++, (f, s) => warnings++);
				Assert.AreEqual(1, actual.Count());
				Assert.AreEqual(0, errors);
				Assert.AreEqual(0, warnings);
				ResourceItem item = actual.First();
				Assert.AreEqual(parameterCount, item.Parameters!.Count);
				Assert.IsNull(item.LocalizationVariants);
			}
			void error(string path) {
				int errors = 0;
				int warnings = 0;
				IEnumerable<ResourceItem> actual = ResourceParser.Parse(path, true, [], (f, s) => errors++, (f, s) => warnings++);
				Assert.IsTrue(0 < errors);
				Assert.IsFalse(actual.Any());
			}
			
			valid(this.WriteFile(R(R("a", "{0}{{1:D}}", "{int i}"))),				1);
			valid(this.WriteFile(R(R("a", "{0}{{{1:D}}}", "{int i, int j}"))),		2);
			valid(this.WriteFile(R(R("a", "{0}{{{1:D}", "{DateTime i, int j}"))),	2);
			valid(this.WriteFile(R(R("a", "{0}{1:D}}}", "{DateTime i, int j}"))),	2);
			valid(this.WriteFile(R(R("a", "{0}{1:D}", "{DateTime i, int j}"))),		2);
			valid(this.WriteFile(R(R("a", "}}{0}", "{int i}"))),					1);
			valid(this.WriteFile(R(R("a", "a{0,-45:dd:mm:yyyy}", "{int i}"))),		1);
			valid(this.WriteFile(R(R("a", "a {0  ,  -45  :  dd:mm:yyyy  }", "{int i}"))),		1);

			error(this.WriteFile(R(R("a", "{0:ddddd}}}}MMMMM}", "{DateTime i}"))));
			error(this.WriteFile(R(R("a", "{0:ddddd}}a}MMMMM", "{DateTime i}"))));
			error(this.WriteFile(R(R("a", "{0:ddddd}}MMMMM}", "{DateTime i}"))));
			error(this.WriteFile(R(R("a", "{0:ddddd}}a}MMMMM}", "{DateTime i}"))));
			error(this.WriteFile(R(R("a", "{0:ddddd}}}MMMMM}", "{DateTime i}"))));
			error(this.WriteFile(R(R("a", "}{0}", "{int i}"))));
			error(this.WriteFile(R(R("a", "a}{0}", "{int i}"))));
			error(this.WriteFile(R(R("a", "{}0}", "{int i}"))));
			error(this.WriteFile(R(R("a", "{0", "{int i}"))));
		}

		[TestMethod]
		public void SatellitesValidationTest() {
			string main = this.WriteFile(R(
				R("a", "b", null),
				R("b", "{0}", "{int i}"),
				R("c", "{0}{1}{2}", "{int i, int j, int k}"),
				R("d", "abc", "!(abc, def, ghi)")
			));
			const int count = 4;
			void valid(string name, string value) {
				string path = this.WriteFile(R(R(name, value, null)));
				int errors = 0;
				int warnings = 0;
				IEnumerable<ResourceItem> actual = ResourceParser.Parse(main, true, [path], (f, s) => errors++, (f, s) => warnings++);
				Assert.AreEqual(count, actual.Count());
				Assert.AreEqual(0, errors);
				Assert.AreEqual(0, warnings);
			}
			void warning(string name, string value) {
				string path = this.WriteFile(R(R(name, value, null)));
				int errors = 0;
				int warnings = 0;
				void err(string? file, string message) {
					this.TestContext.WriteLine($"Error in file {file}: {message}");
					errors++;
				}
				void warn(string? file, string message) {
					this.TestContext.WriteLine($"Warning in file {file}: {message}");
					warnings++;
				}
				IEnumerable<ResourceItem> actual = ResourceParser.Parse(main, false, [path], err, warn);
				Assert.AreEqual(count, actual.Count());
				Assert.AreEqual(0, errors);
				Assert.IsTrue(0 < warnings);
			}
			void error(string name, string value) {
				string path = this.WriteFile(R(R(name, value, null)));
				int errors = 0;
				int warnings = 0;
				IEnumerable<ResourceItem> actual = ResourceParser.Parse(main, true, [path], (f, s) => errors++, (f, s) => warnings++);
				Assert.IsFalse(actual.Any());
				Assert.IsTrue(0 < errors);
			}

			valid("a", "b2");
			warning("a", "{0}");
			valid("b", "d{0}d");
			valid("c", "a{1}b{0}c{2}d");

			warning("e", "f");
			error("d", "zxc");
			error("c", "{1}");
		}

		[TestMethod]
		public void FormatStringsTest() {
			void valid(string name, string value, string? comment) {
				string file = this.WriteFile(R(R(name, value, comment)));
				IEnumerable<ResourceItem> actual = ResourceParser.Parse(file, true, [], this.ThrowOnErrorMessage, this.ThrowOnErrorMessage);
				Assert.AreEqual(1, actual.Count());
			}
			void error(string name, string value, string? comment) {
				string file = this.WriteFile(R(R(name, value, comment)));
				int errors = 0;
				void onError(string? file, string message) {
					this.TestContext.WriteLine($"Error in file {file}: {message}");
					errors++;
				}
				IEnumerable<ResourceItem> actual = ResourceParser.Parse(file, true, [], onError, this.ThrowOnErrorMessage);
				Assert.IsFalse(actual.Any());
				Assert.IsTrue(0 < errors);
			}
			
			valid("a", "{0:dd MMMM yyyy}", "{DateTime i}");
			valid("a", "{0:dd MMMM yyyy}", "{System.DateTime i}");
			valid("a", "{0:O}", "{DateTimeOffset i}");
			valid("a", "{0:O}", "{System.DateTimeOffset i}");

			error("a", "{0:k}", "{DateTime i}");

			valid("a", "{0:d}", "{byte i}");
			valid("a", "{0:d}", "{sbyte i}");
			valid("a", "{0:d}", "{short i}");
			valid("a", "{0:d}", "{Int16 i}");
			valid("a", "{0:d}", "{int i}");
			valid("a", "{0:d}", "{Int32 i}");
			valid("a", "{0:d} {0:g} {0:f}", "{Int16 i}");

			error("a", "{0:k}", "{int i}");

			valid("a", "{0:d}", "{Guid g}");
			valid("a", "{0:P}", "{System.Guid g}");

			error("a", "{0:k}", "{Guid i}");

			valid("a", "{0:g}", "{TimeSpan ts}");
			valid("a", "{0:%m' min.'}", "{TimeSpan ts}");

			error("a", "{0:z}", "{TimeSpan ts}");

			valid("a", "{0:x}", "{MyEnum me}");

			error("a", "{0:s}", "{MyEnum me}");
		}
	}
}
