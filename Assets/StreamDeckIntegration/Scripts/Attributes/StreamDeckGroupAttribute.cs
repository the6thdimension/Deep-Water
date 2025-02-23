using System;
using JetBrains.Annotations;

namespace F10.StreamDeckIntegration.Attributes {
	/// <summary>
	/// Attribute to mark specific classes with all it's members as executable by Stream Deck.
	/// </summary>
	[PublicAPI]
	[MeansImplicitUse]
	[AttributeUsage(AttributeTargets.Class)]
	public class StreamDeckGroupAttribute : Attribute {

		internal string Id { get; private set; }

		/// <summary>
		/// Marks all the fields, properties and methods inside the class as executable by the Stream Deck.
		/// </summary>
		/// <param name="id">Custom ID. Defaults to the name of the class.</param>
		public StreamDeckGroupAttribute(string id = null) {
			Id = id;
		}
	}
}