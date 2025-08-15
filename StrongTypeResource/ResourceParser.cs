using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace StrongTypeResource {
	/// <summary>
	/// Parses and creates list of Items for generator to produce code.
	/// Expect the following syntax on comment:
	/// - minus in the first position of the comment turn off any parsing and property will be generated.
	/// !(value1, value2, ... valueN) list of allowed items. The value of the resource is expected to be one of the value1-valueN
	/// If there is no formating parameters than comment is ignored
	/// If there are formating parameters comment should declare parameters of formating function: {type1 parameter1, type2 parameter2, ... typeM parameterM}
	/// </summary>
	internal sealed class ResourceParser {
		private static readonly char[] splitter = { ',' };

		public static IEnumerable<ResourceItem> Parse(string file, bool enforceParameterDeclaration, IEnumerable<string> satellites, Action<string> errorMessage, Action<string> warningMessage) {
			ResourceParser parser = new ResourceParser(enforceParameterDeclaration, satellites, errorMessage, warningMessage);

			List<ResourceItem> list = new List<ResourceItem>();
			void assign(ResourceItem? item) { if(item != null) { list.Add(item); } }
			parser.Parse(file,
				(string name, string value, string comment) => assign(parser.GenerateInclude(name, value, comment)),
				(string name, string value, string comment) => assign(parser.GenerateString(name, value, comment))
			);
			if(parser.errorCount == 0 && parser.satellites.Any()) {
				parser.VerifySatellites(file, list);
			}
			if(parser.errorCount == 0) {
				return list;
			}
			return Enumerable.Empty<ResourceItem>();
		}

		private string currentFile;
		private readonly bool enforceParameterDeclaration;
		private readonly IEnumerable<string> satellites;

		private const RegexOptions regexOptions = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline;
		private readonly Regex variantList = new Regex(@"^!\((?<list>.*)\)", regexOptions);
		// {int index, string message} hello, world {System.Int32 param} comment {} {MyType value1, Other value2, OneMore last}
		private readonly Regex functionParameters = new Regex(@"\{(?<param>[^}]+)\}", regexOptions);
		// a.b.c.d a, int i, string text, System.Int32 index
		private readonly Regex parameterDeclaration = new Regex(@"^(?<type>[A-Za-z_][A-Za-z_0-9]*(\s*\.\s*[A-Za-z_][A-Za-z_0-9]*)*)\s+(?<name>[A-Za-z_][A-Za-z_0-9]*)$", regexOptions);

		private int errorCount;
		private int warningCount;
		private readonly Action<string> errorMessage;
		private readonly Action<string> warningMessage;

		private ResourceParser(bool enforceParameterDeclaration, IEnumerable<string> satellites, Action<string> errorMessage, Action<string> warningMessage) {
			this.currentFile = string.Empty; // will be set in Parse
			this.enforceParameterDeclaration = enforceParameterDeclaration;
			this.satellites = satellites;
			this.errorCount = 0;
			this.warningCount = 0;
			this.errorMessage = errorMessage;
			this.warningMessage = warningMessage;
		}

		private void Parse(string file, Action<string, string, string> generateInclude, Action<string, string, string> generateString) {
			XmlReaderSettings xmlReaderSettings = new XmlReaderSettings() {
				CloseInput = true,
				IgnoreComments = true,
				IgnoreProcessingInstructions = true,
				IgnoreWhitespace = true,
				DtdProcessing = DtdProcessing.Prohibit, // we don't use DTD. Let's prohibit it for better security
				XmlResolver = null // no external resources are allowed
			};
			this.currentFile = file;
			using XmlReader reader = XmlReader.Create(file, xmlReaderSettings);
			reader.MoveToContent();
			if(reader.NodeType != XmlNodeType.Element || reader.Name != "root") {
				this.Error("root", "Root element is not <root>");
				return;
			}
			while(reader.Read()) {
				if(reader.NodeType == XmlNodeType.Element && reader.Name == "data") {
					// Read <data> node
					string? name = null;
					string? type = null;
					while(reader.MoveToNextAttribute()) {
						if(reader.Name == "name") {
							name = reader.Value.Trim();
						} else if(reader.Name == "type") {
							type = reader.Value.Trim();
						}
					}
					if(name == null) {
						this.Error("data", "Resource name is missing");
						continue;
					}
					if(reader.IsEmptyElement) {
						this.Error(name, "Resource value is missing");
						continue;
					}
					string? value = null;
					string? comment = null;
					if(reader.Read()) {
						while(!(reader.NodeType == XmlNodeType.EndElement && reader.Name == "data")) {
							if(reader.NodeType == XmlNodeType.Element) {
								if(reader.Name == "value") {
									if(value != null) {
										this.Error(name, "Resource value is duplicated: " + value);
									}
									value = reader.ReadElementContentAsString(); // move to the next node
								} else if(reader.Name == "comment") {
									if(comment != null) {
										this.Error(name, "Resource comment is duplicated: " + comment);
									}
									comment = reader.ReadElementContentAsString(); // move to the next node
								} else {
									this.Warning(name, "Unexpected node: " + reader.Name);
									reader.ReadElementContentAsString(); // just skip this node
								}
							} else {
								reader.Skip(); // skip text nodes and other nodes
							}
						}
					}
					if(value == null) {
						this.Error(name, "Resource value is missing");
						continue;
					}
					if(comment == null) {
						comment = string.Empty; // no comment is ok
					}
					if(type != null) {
						// It is an include
						generateInclude(name, value, comment);
					} else {
						// It is a string
						generateString(name, value, comment);
					}
				}
			}
		}

		private void VerifySatellites(string mainFile, List<ResourceItem> itemList) {
			Dictionary<string, ResourceItem> items = new Dictionary<string, ResourceItem>(itemList.Count);
			itemList.ForEach(i => items.Add(i.Name, i));
			void unknownResource(string name) => this.Warning(name, "resource provided in language resource file \"{0}\" does not exist in the main resource file \"{1}\"", this.currentFile, mainFile);
			foreach(string file in this.satellites) {
				this.Parse(file,
					(string name, string value, string comment) => {
						if(items.TryGetValue(name, out ResourceItem? item)) {
							ResourceItem? satellite = this.GenerateInclude(name, value, comment);
							// satellite == null on errors. So, do not generate yet another one.
							if(satellite != null && item.Type != satellite.Type) {
								this.Error(name, "types of resources are different in main resource file \"{0}\" and language resource file \"{1}\"", mainFile, file);
							}
						} else {
							unknownResource(name);
						}
					},
					(string name, string value, string comment) => {
						if(items.TryGetValue(name, out ResourceItem? item)) {
							if(!item.SuppressValidation) {
								int count = this.ValidateFormatItems(name, value, false);
								if(count != (item.Parameters == null ? 0 : item.Parameters.Count)) {
									this.Warning(name, "numbers of format parameters are different in the main resource file \"{0}\" and language resource file \"{1}\"", mainFile, this.currentFile);
								} else if(item.LocalizationVariants != null) {
									if(!item.LocalizationVariants.Contains(value)) {
										this.Error(name, "provided value '{0}' in language resource file \"{1}\" doesn't match any of the allowed options defined in main resource file: \"{2}\"", value, this.currentFile, mainFile);
									}
								}
							}
						} else {
							unknownResource(name);
						}
					}
				);
			}
		}

		private bool Error(string nodeName, string errorText, params object[] args) {
			//"C:\Projects\TestApp\TestApp\Subfolder\TextMessage.resx(10,1): error URW001: nodeName: my error"
			this.errorMessage($"{this.currentFile}(1,1): error URW001: {nodeName}: {ResourceParser.Format(errorText, args)}");
			this.errorCount++;
			return false;
		}

		private void Warning(string nodeName, string errorText, params object[] args) {
			this.warningMessage($"{this.currentFile}(1,1): warning: {nodeName}: {ResourceParser.Format(errorText, args)}");
			this.warningCount++;
		}

		private bool Corrupted(string nodeName) {
			return this.Error(nodeName, "Structure of the value node is corrupted");
		}

		private static string Format(string format, params object[] args) {
			return string.Format(CultureInfo.InvariantCulture, format, args);
		}

		private ResourceItem? GenerateInclude(string name, string value, string comment) {
			string[] list = value.Split(';');
			if(list.Length < 2) {
				this.Corrupted(name);
				return null;
			}
			string file = list[0];
			list = list[1].Split(',');
			if(list.Length < 2) {
				this.Corrupted(name);
				return null;
			}
			string type = list[0].Trim();
			if(0 == this.errorCount) {
				file = ResourceParser.Format("content of the file: \"{0}\"", file);
				return new ResourceItem(name, file, type);
			}
			return null;
		}

		private ResourceItem? GenerateString(string name, string value, string comment) {
			ResourceItem item = new ResourceItem(name, value, "string");

			if(!comment.StartsWith("-", StringComparison.Ordinal)) {
				if(!this.IsVariantList(item, comment)) {
					this.ParseFormatParameters(item, comment);
				}
			} else {
				item.SuppressValidation = true;
			}

			return (0 == this.errorCount) ? item : null;
		}

		private bool IsVariantList(ResourceItem item, string comment) {
			Match match = this.variantList.Match(comment);
			if(match.Success) {
				string listText = match.Groups["list"].Value;
				string[] variants = listText.Split(',');
				List<string> list = new List<string>();
				foreach(string var in variants) {
					string text = var.Trim();
					if(0 < text.Length) {
						list.Add(text);
					}
				}
				item.LocalizationVariants = list;
				if(!list.Contains(item.Value)) {
					this.Error(item.Name, "The provided value '{0}' is not one of the allowed translation options.", item.Value);
				}
			}
			return match.Success;
		}

		private void ParseFormatParameters(ResourceItem item, string comment) {
			int count = this.ValidateFormatItems(item.Name, item.Value, true);
			if(0 < count) {
				Match paramsMatch = this.functionParameters.Match(comment);
				if(paramsMatch.Success) {
					string[] list = paramsMatch.Groups["param"].Value.Split(ResourceParser.splitter, StringSplitOptions.RemoveEmptyEntries);
					List<Parameter> parameterList = new List<Parameter>(list.Length);
					foreach(string text in list) {
						if(!string.IsNullOrWhiteSpace(text)) {
							Match parameterMatch = this.parameterDeclaration.Match(text.Trim());
							if(parameterMatch.Success) {
								parameterList.Add(new Parameter(parameterMatch.Groups["type"].Value, parameterMatch.Groups["name"].Value));
							} else {
								this.Error(item.Name, "bad parameter declaration: {0}", text.Trim());
							}
						}
					}
					if(parameterList.Count != count) {
						this.Error(item.Name, "number of parameters expected by value of the string do not match to provided parameter list in comment");
					}
					item.Parameters = parameterList;
				} else {
					string error = "string value contains formating placeholders, but function parameters declaration is missing in the comment.";
					if(this.enforceParameterDeclaration) {
						this.Error(item.Name, error);
					} else {
						this.Warning(item.Name, error);
					}
				}
			}
		}

		/// <summary>
		/// Validates format string in a manner very close to what string.Format do.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
		/// <param name="requareAllParameters"></param>
		/// <returns></returns>
		private int ValidateFormatItems(string name, string value, bool requareAllParameters) {
			int error() {
				this.Error(name, "Invalid formating item.");
				return -1;
			}
			HashSet<int> indexes = new HashSet<int>();
			for(int i = 0; i < value.Length; i++) {
				if('}' == value[i]) {
					i++;
					if(!(i < value.Length && '}' == value[i])) {
						this.Error(name, "Input string is not in correct format");
						return -1;
					}
				} else if('{' == value[i]) {
					i++;
					if(i < value.Length && '{' == value[i]) {
						continue; // skip escaped {
					}
					// Formating item is started
					// First is parameter number. Spaces are not allowed in front or it.
					bool isNumber = false;
					int index = 0;
					while(i < value.Length && '0' <= value[i] && value[i] <= '9' && index < 1000000) {
						index = index * 10 + value[i] - '0';
						isNumber = true;
						i++;
					}
					if(!isNumber || 1000000 <= index) {
						return error();
					}
					indexes.Add(index);
					//Skip spaces
					while(i < value.Length && ' ' == value[i]) {
						i++;
					}
					//Check for alignment
					if(i < value.Length && ',' == value[i]) {
						i++;
						while(i < value.Length && ' ' == value[i]) {
							i++;
						}
						if(i < value.Length && '-' == value[i]) {
							i++; //skip sign
						}
						isNumber = false;
						index = 0;
						while(i < value.Length && '0' <= value[i] && value[i] <= '9' && index < 1000000) {
							index = index * 10 + value[i] - '0';
							isNumber = true;
							i++;
						}
						if(!isNumber || 1000000 <= index) {
							return error();
						}
					}
					//Skip spaces
					while(i < value.Length && ' ' == value[i]) {
						i++;
					}
					//Check for format string.
					if(i < value.Length && ':' == value[i]) {
						// Inside format string. It is allowed to have escaped open and closed braces, so skip them until single }
						for(;;) {
							i++;
							while(i < value.Length && '}' != value[i]) {
								if('{' == value[i]) {
									i++;
									if(!(i < value.Length && '{' == value[i])) {
										return error();
									}
								}
								i++;
							}
							if(i + 1 < value.Length && '}' == value[i + 1]) {
								i++;
							} else {
								break;
							}
						}
					}
					if(!(i < value.Length && '}' == value[i])) {
						return error();
					}
				}
			}
			// Check that all format parameters are present.
			int current = 0;
			foreach(int index in indexes.OrderBy(i => i)) {
				if(index != current++) {
					if(requareAllParameters) {
						this.Error(name, "parameter number {0} is missing in the string", current - 1);
					} else {
						this.Warning(name, "parameter number {0} is missing in the string", current - 1);
						break;
					}
					return -1; // report just one missing parameter number
				}
			}
			return indexes.Count;
		}
	}
}
