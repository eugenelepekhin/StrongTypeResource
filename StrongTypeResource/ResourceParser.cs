using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
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
		public static IEnumerable<ResourceItem> Parse(string file, bool enforceParameterDeclaration, IEnumerable<string> satellites, Action<string?, string> errorMessage, Action<string?, string> warningMessage) {
			ResourceParser parser = new ResourceParser(enforceParameterDeclaration, errorMessage, warningMessage);

			List<ResourceItem> list = new List<ResourceItem>();
			void assign(ResourceItem? item) { if(item != null) { list.Add(item); } }
			parser.Parse(file,
				(string name, string value) => assign(parser.GenerateInclude(name, value)),
				(string name, string value, string? comment) => assign(parser.GenerateString(name, value, comment))
			);
			if(parser.errorCount == 0 && satellites.Any()) {
				parser.VerifySatellites(Path.GetFileName(file), list, satellites);
			}
			if(parser.errorCount == 0) {
				return list;
			}
			return Enumerable.Empty<ResourceItem>();
		}

		private string currentFile;
		private readonly bool enforceParameterDeclaration;

		private readonly ResourceItem.Parser parser;

		private int errorCount;
		private readonly Action<string?, string> errorMessage;
		private readonly Action<string?, string> warningMessage;

		private ResourceParser(bool enforceParameterDeclaration, Action<string?, string> errorMessage, Action<string?, string> warningMessage) {
			this.currentFile = string.Empty; // will be set in Parse
			this.enforceParameterDeclaration = enforceParameterDeclaration;
			this.errorCount = 0;
			this.errorMessage = errorMessage;
			this.warningMessage = warningMessage;
			this.parser = new ResourceItem.Parser(
				(string nodeName, string message) => this.Error(nodeName, message),
				(string nodeName, string message) => this.Warning(nodeName, message)
			);
		}

		private void Parse(string file, Action<string, string> generateInclude, Action<string, string, string?> generateString) {
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
				this.Error(reader.Name, "Root element is not <root>");
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
						this.Error(name, "resource value is missing");
						continue;
					}
					string? value = null;
					string? comment = null;
					if(reader.Read()) {
						while(!(reader.NodeType == XmlNodeType.EndElement && reader.Name == "data")) {
							if(reader.NodeType == XmlNodeType.Element) {
								if(reader.Name == "value") {
									if(value != null) {
										this.Error(name, "resource value is duplicated: " + value);
									}
									value = reader.ReadElementContentAsString(); // move to the next node
								} else if(reader.Name == "comment") {
									if(comment != null) {
										this.Error(name, "resource comment is duplicated: " + comment);
									}
									comment = reader.ReadElementContentAsString(); // move to the next node
								} else {
									this.Warning(name, "unexpected node: " + reader.Name);
									reader.ReadElementContentAsString(); // just skip this node
								}
							} else {
								reader.Skip(); // skip text nodes and other nodes
							}
						}
					}
					if(value == null) {
						this.Error(name, "resource value is missing");
						continue;
					}
					if(type != null) {
						// It is an include
						generateInclude(name, value);
					} else {
						// It is a string
						generateString(name, value, comment);
					}
				}
			}
		}

		private void VerifySatellites(string mainFile, List<ResourceItem> itemList, IEnumerable<string> satellites) {
			Dictionary<string, ResourceItem> items = new Dictionary<string, ResourceItem>(itemList.Count);
			itemList.ForEach(i => items.Add(i.Name, i));
			void unknownResource(string name) => this.Warning(name, "provided resource does not exist in the main resource file \"{0}\".", mainFile);
			foreach(string file in satellites) {
				this.Parse(file,
					(string name, string value) => {
						if(items.TryGetValue(name, out ResourceItem? item)) {
							ResourceItem? satellite = this.GenerateInclude(name, value);
							// satellite == null on errors. So, do not generate yet another one.
							if(satellite != null && item.Type != satellite.Type) {
								this.Error(name, "has a different type than what is defined in the main resource file \"{0}\".", mainFile);
							}
						} else {
							unknownResource(name);
						}
					},
					(string name, string value, string? comment) => {
						if(items.TryGetValue(name, out ResourceItem? item)) {	
							this.ValidateString(item, value, mainFile);
						} else {
							unknownResource(name);
						}
					}
				);
			}
		}

		private void Error(string nodeName, string errorText, params object[] args) {
			//"C:\Projects\TestApp\TestApp\Subfolder\TextMessage.resx(10,1): error: nodeName: my error"
			this.errorMessage(this.currentFile, $"'{nodeName}' {ResourceParser.Format(errorText, args)}");
			this.errorCount++;
		}

		private void Warning(string nodeName, string errorText, params object[] args) {
			this.warningMessage(this.currentFile, $"'{nodeName}' {ResourceParser.Format(errorText, args)}");
		}

		private static string Format(string format, params object[] args) {
			return string.Format(CultureInfo.InvariantCulture, format, args);
		}

		private static string MainFileReference(string? prefix, string? fileName) {
			return (fileName != null) ? ResourceParser.Format("{0} in main resource file: \"{1}\"", prefix ?? string.Empty, Path.GetFileName(fileName)) : string.Empty;
		}

		private ResourceItem? GenerateInclude(string name, string value) {
			void corrupted(string nodeName) => this.Error(nodeName, "structure of the value node is corrupted.");
			string[] list = value.Split(';');
			if(list.Length < 2) {
				corrupted(name);
				return null;
			}
			string file = list[0];
			list = list[1].Split(',');
			if(list.Length < 2) {
				corrupted(name);
				return null;
			}
			string type = list[0].Trim();
			if(0 == this.errorCount) {
				file = ResourceParser.Format("content of the file: \"{0}\"", file);
				return new ResourceItem(name, file, type);
			}
			return null;
		}

		private ResourceItem? GenerateString(string name, string value, string? comment) {
			ResourceItem item = new ResourceItem(name, value, "string");
			if(item.ParseComment(this.parser, comment)) {
				this.ValidateString(item, value, null);
			}
			return (0 == this.errorCount) ? item : null;
		}

		private void ValidateString(ResourceItem item, string value, string? mainFile) {
			if(!item.SuppressValidation) {
				if(item.IsEnumeration) {
					if(!item.IsValidEnumerationOption(value)) {
						this.Error(item.Name, "provided value '{0}' is not in the list of allowed options: ({1}){2}.", value, string.Join(", ", item.LocalizationVariants), MainFileReference(" defined", mainFile));
					}
				} else {
					Dictionary<int, List<string>?> usedIndexes = new();
					if(this.ParseFormatItems(item, value, usedIndexes)) {
						if(0 < usedIndexes.Count) {
							if(!item.IsFunction) {
								string error = Format("string value contains formating placeholders, but the function parameters declaration is missing in the comment{0}.", MainFileReference(null, mainFile));
								if(this.enforceParameterDeclaration) {
									this.Error(item.Name, error);
								} else {
									this.Warning(item.Name, error);
								}
							} else if(item.Parameters!.Count != usedIndexes.Count) {
								this.Error(item.Name, "the number of format placeholders in the string doesn't match number of parameters listed in the comment{0}", MainFileReference(null, mainFile));
							} else {
								for(int i = 0; i < usedIndexes.Count; i++) {
									if(!usedIndexes.TryGetValue(i, out List<string>? formats)) {
										this.Error(item.Name, "parameter #{0} is not used in the format string", i);
									} else if(formats != null && !formats.Any(f => item.IsValidFormat(i, f))) {
										this.Error(item.Name, "format item #{0} has invalid format string in {1}", i, value);
									}
								}
							}
						} else if(item.IsFunction) {
							this.Error(item.Name, "no format items are used in the value, but function parameters are declared in the comment{0}.", MainFileReference(null, mainFile));
						}
					}
				}
			}
		}

		[SuppressMessage("Performance", "CA1854:Prefer the 'IDictionary.TryGetValue(TKey, out TValue)' method")]
		private bool ParseFormatItems(ResourceItem item, string value, Dictionary<int, List<string>?> usedIndexes) {
			bool invalid() { this.Error(item.Name, "invalid format item in {0}", value); return false; }
			int position = 0;
			int length = value.Length;
			bool isEos() => length <= position;
			void next() => position++;
			int current() {
				if(isEos()) return -1;
				return value[position];
			}
			void skipWhitespace() {
				while(!isEos() && char.IsWhiteSpace((char)current())) {
					next();
				}
			}
			while(!isEos()) {
				if(current() == '{') {
					next();
					if(!isEos()) {
						if(current() != '{') { // skip escaped opening braces outside of a format item
							// Start of a format item
							int index = 0;
							bool isNumber = false;
							while('0' <= current() && current() <= '9') {
								isNumber = true;
								index = index * 10 + (current() - '0');
								if(1_000_000 <= index) return invalid();
								next();
							}
							if(!isNumber) return invalid();
							if(!usedIndexes.ContainsKey(index)) {
								usedIndexes.Add(index, null);
							}
							skipWhitespace();
							if(current() == ',') {
								next(); // skip comma
								// width part of the format item
								skipWhitespace();
								if(current() == '-') next(); // skip sign. note there is not + sign in .net parsing
								isNumber = false;
								int width = 0;
								while('0' <= current() && current() <= '9') {
									isNumber = true;
									width = width * 10 + (current() - '0');
									if(1_000_000 <= width) return invalid();
									next();
								}
								if(!isNumber) return invalid();
								skipWhitespace();
							}
							if(current() == ':') {
								// format string of the format item. in .net core doesn't allow escaped closing braces. so grab everything until the next closing brace
								// .net framework allows escaped braces, but this producing ambiguous results, so we don't support it.
								int start = position + 1;
								while(!isEos() && current() != '}') {
									if(current() == '{') return invalid(); // no open braces allowed in format string
									next();
								}
								if(current() != '}' || position == start) return invalid();
								List<string>? formats = usedIndexes[index];
								if(formats == null) {
									formats = new List<string>();
									usedIndexes[index] = formats;
								}
								formats.Add(value.Substring(start, position - start));
							}
							if(current() != '}') return invalid();
						}
					} else {
						return invalid();
					}
				} else if(current() == '}') {
					next();
					if(isEos() || current() != '}') return invalid(); // Allow escaped closing braces outside of a format item
				}
				next();
			}
			return true;
		}
	}
}
