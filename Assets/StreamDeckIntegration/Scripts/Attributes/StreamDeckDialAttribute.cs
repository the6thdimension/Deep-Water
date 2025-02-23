using System;
using JetBrains.Annotations;

namespace F10.StreamDeckIntegration.Attributes {
	/// <summary>
	/// Attribute to mark specific methods as executable by Stream Deck dials.
	/// </summary>
	[PublicAPI]
	[MeansImplicitUse]
	[AttributeUsage(AttributeTargets.Method)]
	public class StreamDeckDialAttribute : Attribute {

		internal string Id { get; private set; }

		/// <summary>
		/// Marks this field, property or method as executable by the Stream Deck.
		/// </summary>
		/// <param name="id">Custom ID. Defaults to the name of the member.</param>
		public StreamDeckDialAttribute(string id = null) {
			Id = id;
		}

	}
}