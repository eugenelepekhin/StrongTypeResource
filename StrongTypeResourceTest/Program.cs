using System.Text.RegularExpressions;

namespace StrongTypeResourceTest {
	internal static class Program {
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2241:Provide correct arguments to formatting methods", Justification = "<Pending>")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
		static void Main(string[] args) {
			//Resources.Text.Culture = System.Globalization.CultureInfo.GetCultureInfo("ru");
			Console.WriteLine(Resources.Text.Greetings);
			Console.WriteLine(Resources.Text.OtherMessage(42));
			string s = NewResx.Resources.String1;
			Console.WriteLine(NewResx.Resources.String1);
			//Console.WriteLine("{ 0: d \n }", 42); // this will throw in .net parsing and bring source code.
		}
	}
}
