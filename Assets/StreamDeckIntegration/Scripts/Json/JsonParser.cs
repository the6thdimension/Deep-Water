using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace F10.StreamDeckIntegration.Json {
	/// <summary>
	/// Simplified JSON parser to exclusively deal with StreamDeck JSON websocket communication messages.
	/// </summary>
	internal static class JsonParser {

		[CanBeNull]
		internal static Dictionary<string, JsonData> Parse(string json) {
			if (!json.StartsWith("{") || !json.EndsWith("}")) return null;

			var dict = new Dictionary<string, JsonData>();

			// Remove start and end {}
			json = json.Substring(1);
			json = json.Remove(json.Length - 1);
			
			// Encode all characters between quotes
			json = EncodeInQuotes(json);

			// Separate by , (taking care of nested {})
			var lastIndex = 0;
			var nestedLevel = 0;
			for (var i = 0; i < json.Length; i++) {
				if (i >= json.Length - 1 || json[i] == ',' && nestedLevel <= 0) {
					// Separate key-value :
					var section = json.Substring(lastIndex, (i + 1) - lastIndex);

					// Remove start and end ,
					if (section.StartsWith(",")) {
						section = section.Substring(1);
					}

					if (section.EndsWith(",")) {
						section = section.Remove(section.Length - 1);
					}

					// Split by :
					var keyValue = section.Split(new[] {':'}, 2).Select(x => x.Trim()).ToArray();

					// Has nested json?
					var jsonData = keyValue[1].Contains("{")
						? new JsonData(Parse(Uri.UnescapeDataString(keyValue[1])))
						: new JsonData(RemoveQuotesAndDecode(keyValue[1]));
					
					dict.Add(RemoveQuotesAndDecode(keyValue[0]), jsonData);
					lastIndex = i;
					continue;
				}

				switch (json[i]) {
					case '{':
						nestedLevel++;
						continue;
					case '}':
						nestedLevel--;
						continue;
				}
			}

			return dict;
		}
		
		private static string EncodeInQuotes(string data) {
			const string pattern = "\"([^\"]*)\"";
			return Regex.Replace(data, pattern, Encode, RegexOptions.IgnoreCase);
		}
		
		private static string Encode(Match m) {
			return "\"" + Uri.EscapeDataString(m.Groups[1].Value) + "\"";
		}

		// Remove start and end quotes, decode
		private static string RemoveQuotesAndDecode(string data) {
			if (data.StartsWith("\"")) {
				data = data.Substring(1);
			}
			if (data.EndsWith("\"")) {
				data = data.Remove(data.Length - 1);
			}
			return Uri.UnescapeDataString(data);
		}

	}
}