namespace StrongTypeResourceTest {
	internal static class Program {
		static void Main(string[] args) {
			//Resources.Text.Culture = System.Globalization.CultureInfo.GetCultureInfo("ru");
			Console.WriteLine(Resources.Text.Greetings);
			Console.WriteLine(Resources.Text.OtherMessage(42));
			string s = NewResx.Resources.String1;
			Console.WriteLine(NewResx.Resources.String1);
		}
	}
}
