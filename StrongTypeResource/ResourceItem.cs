using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace StrongTypeResource {
	internal sealed class ResourceItem {
		internal sealed class Parser {
			private const RegexOptions regexOptions = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline;
			// - suppress validation of item
			private readonly Regex suppressValidation = new Regex(@"^\s*-", regexOptions);
			// !(hello, world)
			private readonly Regex variantList = new Regex(@"^\s*!\((?<list>.*?)\)", regexOptions);
			// {int index, string message} hello, world {System.Int32 param} comment {} {MyType value1, Other value2, OneMore last}
			private readonly Regex parameterList = new Regex(@"^\s*\{(?<param>[^}]+)\}", regexOptions);
			// a.b.c.d a, int i, string text, System.Int32 index, MyType? value
			private readonly Regex parameterDeclaration = new Regex(@"^\s*(?<type>[\p{L}_@][\p{L}\p{Nd}_]*(\s*\.\s*[\p{L}_@][\p{L}\p{Nd}_]*)*\s*(\??))\s+(?<name>[\p{L}_@][\p{L}\p{Nd}_]*)\s*$", regexOptions);
			// any space characters
			private readonly Regex space = new Regex(@"\s+", regexOptions);


			/// <summary>
			/// Gets the delegate that reports error messages by providing the name of the resource and a descriptive message.
			/// void Error(string name, string message);
			/// </summary>
			public Action<string, string> Error { get; }

			/// <summary>
			/// Gets the delegate that reports warning messages by providing the name of the resource and a descriptive message.
			/// void Warning(string name, string message);
			/// </summary>
			public Action<string, string> Warning { get; }

			public Parser(Action<string, string> error, Action<string, string> warning) {
				this.Error = error;
				this.Warning = warning;
			}

			public bool ParseComment(string name, string? comment, out bool suppressValidation, out IList<string>? localizationVariants, out IList<Parameter>? parameters) {
				suppressValidation = false;
				localizationVariants = null;
				parameters = null;

				if(string.IsNullOrWhiteSpace(comment)) {
					return true; // No comment to parse
				}

				// Check for suppression of validation
				if(this.suppressValidation.IsMatch(comment)) {
					suppressValidation = true;
					return true; // Suppression found, no further parsing needed
				}

				// Check for localization variants
				Match match = this.variantList.Match(comment);
				if(match.Success) {
					string listText = match.Groups["list"].Value.Trim();
					string[] variants = listText.Split(',');
					List<string> list = new List<string>();
					foreach(string var in variants) {
						string text = var.Trim();
						if(0 < text.Length) {
							list.Add(text);
						}
					}
					localizationVariants = list;
					return true; // Localization variants found
				}

				Match paramsList = this.parameterList.Match(comment);
				if(paramsList.Success) {
					string[] list = paramsList.Groups["param"].Value.Split(',');
					List<Parameter> parameterList = new List<Parameter>(list.Length);
					foreach(string text in list) {
						Match parameterMatch = this.parameterDeclaration.Match(text);
						if(parameterMatch.Success) {
							parameterList.Add(new Parameter(this.space.Replace(parameterMatch.Groups["type"].Value, string.Empty), parameterMatch.Groups["name"].Value));
						} else {
							this.Error(name, $"bad parameter declaration: {text.Trim()}");
							return false; // Invalid parameter declaration
						}
					}
					if(0 < parameterList.Count) {
						parameters = parameterList;
						return true; // Parameters found and parsed
					} else {
						this.Error(name, $"invalid parameter declaration found in the comment: {comment!.Trim()}");
						return false; // No parameters found
					}
				}

				return true; // No parameters or localization variants found, comment is valid
			}
		}

		public string Name { get; }
		public string Value { get; }
		public string Type { get; }
		/// <summary>
		/// List of parameters if format placeholders are present, null otherwise.
		/// </summary>
		public IList<Parameter>? Parameters { get; private set; }
		/// <summary>
		/// List of variant acceptable for this resource if it specified like: !(one, two, three) null otherwise.
		/// </summary>
		public IList<string>? LocalizationVariants { get; set; }
		/// <summary>
		/// True if minus was in the first character of comment to suppress validation of satellites.
		/// </summary>
		public bool SuppressValidation { get; private set; }
		public bool IsEnumeration => this.LocalizationVariants != null && 0 < this.LocalizationVariants.Count;
		public bool IsFunction => this.Parameters != null && 0 < this.Parameters.Count;

		public ResourceItem(string name, string value, string type) {
			this.Name = name;
			this.Value = value;
			this.Type = type;
		}

		public bool ParseComment(Parser parser, string? comment) {
			if(parser.ParseComment(this.Name, comment, out bool suppressValidation,  out IList<string>? localizationVariants, out IList<Parameter>? parameters)) {
				Debug.Assert(
					localizationVariants == null && parameters == null ||
					!suppressValidation && localizationVariants != null && parameters == null ||
					!suppressValidation && localizationVariants == null && parameters != null,
					"Invalid combination of suppressValidation, localizationVariants and parameters."
				);
				this.SuppressValidation = suppressValidation;
				this.LocalizationVariants = localizationVariants;
				this.Parameters = parameters;
				return true; // Comment parsed successfully
			} else {
				return false; // Error in parsing comment
			}
		}

		/// <summary>
		/// Creates comment string as it should appear in generated wrapper.
		/// </summary>
		/// <returns></returns>
		public string Comment() {
			string[] line = this.Value.Split('\n', '\r');
			if(line != null && line.Length > 0) {
				if(line.Length > 1) {
					StringBuilder comment = new StringBuilder();
					comment.Append(line[0].Trim());
					int max = Math.Min(3, line.Length);
					string format = "\t\t/// {0}";
					for(int i = 1; i < max; i++) {
						string s = line[i].Trim();
						if(s.Length > 0) {
							comment.AppendLine();
							comment.AppendFormat(CultureInfo.InvariantCulture, format, s);
						}
					}
					return comment.ToString();
				}
				return line[0].Trim();
			}
			return string.Empty;
		}

		/// <summary>
		/// Creates parameter declaration for use in wrapper method for resource with format placeholders.
		/// </summary>
		/// <returns></returns>
		public string ParametersDeclaration() {
			Debug.Assert(this.Parameters != null, "There are no parameters");
			StringBuilder parameter = new StringBuilder();
			for(int i = 0; i < this.Parameters!.Count; i++) {
				if(i > 0) {
					parameter.Append(", ");
				}
				parameter.AppendFormat(CultureInfo.InvariantCulture, "{0} {1}", this.Parameters[i].Type, this.Parameters[i].Name);
			}
			return parameter.ToString();
		}

		/// <summary>
		/// Creates string of parameter invocations inside of wrapper function body for resources with format placeholders.
		/// </summary>
		/// <returns></returns>
		public string ParametersInvocation() {
			Debug.Assert(this.Parameters != null, "There are no parameters");
			StringBuilder parameter = new StringBuilder();
			for(int i = 0; i < this.Parameters!.Count; i++) {
				if(i > 0) {
					parameter.Append(", ");
				}
				parameter.Append(this.Parameters[i].Name);
			}
			return parameter.ToString();
		}

		public string GetStringExpression(bool pseudo) {
			if(pseudo && this.LocalizationVariants != null && 0 < this.LocalizationVariants.Count) {
				// In case of pseudo and localization variants
				StringBuilder text = new StringBuilder();
				foreach(string value in this.LocalizationVariants) {
					if(0 < text.Length) {
						text.Append(',');
					}
					text.AppendFormat(CultureInfo.InvariantCulture, "\"{0}\"",
						value.Replace("\"", "\\\"").Replace("\\", "\\\\")
					);
				}
				return "((PseudoResourceManager)ResourceManager).GetBaseString(new string[]{" + text.ToString() + "}, ";
			}
			return "ResourceManager.GetString(";
		}

		public bool IsValidEnumerationOption(string value) {
			if(this.IsEnumeration) {
				value = value.Trim();
				return this.LocalizationVariants!.Any(variant => variant == value);
			}
			return false; // Not a valid enumeration option
		}

		/// <summary>
		/// Checks if the format string part of format item is valid for the parameter at the specified index.
		/// For example, for numerical parameter it checks in {0,5:D} or {0:F2} or {0:G4} for validity of "D", "F2", "G4" format strings.
		/// </summary>
		/// <param name="index">Parameter number</param>
		/// <param name="formatString">format string part of format item</param>
		/// <returns></returns>
		public bool IsValidFormatString(int index, string formatString, Parser parser) {
			if(this.IsFunction && index < this.Parameters!.Count) {
				string type = this.Parameters[index].Type.TrimEnd('?');
				switch(type) {
				case "DateTime":
				case "System.DateTime":
				case "DateTimeOffset":
				case "System.DateTimeOffset":
					return ResourceItem.ValidateDateTime(formatString);

				case "byte":
				case "Byte":
				case "System.Byte":

				case "sbyte":
				case "SByte":
				case "System.SByte":

				case "short":
				case "Int16":
				case "System.Int16":

				case "ushort":
				case "UInt16":
				case "System.UInt16":

				case "int":
				case "Int32":
				case "System.Int32":

				case "uint":
				case "UInt32":
				case "System.UInt32":

				case "long":
				case "Int64":
				case "System.Int64":

				case "ulong":
				case "UInt64":
				case "System.UInt64":

				case "float":
				case "Single":
				case "System.Single":

				case "double":
				case "Double":
				case "System.Double":


				case "decimal":
				case "Decimal":
				case "System.Decimal":

				case "BigInteger":
				case "Numerics.BigInteger":
				case "System.Numerics.BigInteger":
					return ResourceItem.ValidateInt(formatString);

				case "Guid":
				case "System.Guid":
					return ResourceItem.ValidateGuid(formatString);

				case "TimeSpan":
				case "System.TimeSpan":
					return ResourceItem.ValidateTimeSpan(formatString);

				case "string":
				case "String":
				case "System.String":
					// Any format string is valid for string, however there is no reason to have one, so trigger a warning.
					parser.Warning(this.Name,
						$"format specifier ':{formatString}' cannot be used with string parameter '{this.Parameters[index].Name}'. " +
						$"Either remove ':{formatString}' from the format string in: '{this.Value.Replace("{", "{{").Replace("}", "}}")}' " +
						$"or update type of '{this.Parameters[index].Name}' in the comment of the main .resx file."
					);
					return true;

				default:
					return ResourceItem.ValidateEnum(formatString);
				}
			}
			return false;
		}

		private static bool ValidateDateTime(string formatString) {
			try {
				string format = $"{{0:{formatString}}}";
				string result = string.Format(CultureInfo.InvariantCulture, format, DateTime.Now);
				return true; // Valid DateTime format
			} catch (FormatException) {
				return false; // Invalid DateTime format
			}
		}

		private static bool ValidateInt(string formatString) {
			try {
				string format = $"{{0:{formatString}}}";
				string result = string.Format(CultureInfo.InvariantCulture, format, 42);
				return true; // Valid integer format
			} catch (FormatException) {
				return false; // Invalid integer format
			}
		}

		private static bool ValidateGuid(string formatString) {
			try {
				string format = $"{{0:{formatString}}}";
				string result = string.Format(CultureInfo.InvariantCulture, format, Guid.NewGuid());
				return true; // Valid Guid format
			} catch (FormatException) {
				return false; // Invalid Guid format
			}
		}

		private static bool ValidateTimeSpan(string formatString) {
			try {
				string format = $"{{0:{formatString}}}";
				string result = string.Format(CultureInfo.InvariantCulture, format, TimeSpan.FromTicks(12345));
				return true; // Valid TimeSpan format
			} catch (FormatException) {
				return false; // Invalid TimeSpan format
			}
		}

		public static bool ValidateEnum(string formatString) {
			switch(formatString) {
				case "G":
				case "g":
				case "F":
				case "f":
				case "D":
				case "d":
				case "X":
				case "x":
					return true; // Valid enum format
				default:
					return false; // Invalid enum format
			}
		}
	}
}
