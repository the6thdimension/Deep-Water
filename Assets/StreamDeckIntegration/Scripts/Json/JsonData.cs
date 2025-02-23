using System;
using System.Collections.Generic;
using System.Linq;

namespace F10.StreamDeckIntegration.Json {
	/// <summary>
	/// Data schema used by the <see cref="JsonParser"/> to serialize StreamDeck messages.
	/// </summary>
	internal class JsonData {

		private readonly string _value;

        internal string AsString {
            get {
                return _value;
            }
        }

        internal bool AsBool {
            get {
                return new[] { "true", "t", "1", "yes" }.Contains(_value.ToLowerInvariant());
            }
        }

        internal int AsInt {
            get {
                return Convert.ToInt32(_value);
            }
        }

        internal float AsFloat {
            get {
                return Convert.ToSingle(_value);
            }
        }

        internal double AsDouble {
            get {
                return Convert.ToDouble(_value);
            }
        }

        private readonly Dictionary<string, JsonData> _nestedValue;

        internal Dictionary<string, JsonData> AsData {
            get {
                return _nestedValue;
            }
        }

        internal JsonData(string value) {
			_value = value;
		}

		internal JsonData(Dictionary<string, JsonData> nestedValue) {
			_nestedValue = nestedValue;
		}

		internal object AsCastedObject(string typedType) {
			switch (typedType.ToLowerInvariant()) {
				case "int":
					return AsInt;
				case "float":
					return AsFloat;
				case "bool":
					return AsBool;
				case "string":
					return AsString;
			}

			throw new ArgumentOutOfRangeException("typedType", "No type found or supported, named " + typedType);
		}

	}
}