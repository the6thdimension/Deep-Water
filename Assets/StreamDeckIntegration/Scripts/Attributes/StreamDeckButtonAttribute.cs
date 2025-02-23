using System;
using JetBrains.Annotations;

namespace F10.StreamDeckIntegration.Attributes {
	/// <summary>
	/// Attribute to mark specific fields, properties and / or methods as executable by Stream Deck.
	/// </summary>
	[PublicAPI]
	[MeansImplicitUse]
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
	public class StreamDeckButtonAttribute : Attribute {

		internal string Id { get; private set; }

		/// <summary>
		/// Marks this field, property or method as executable by the Stream Deck.
		/// </summary>
		/// <param name="id">Custom ID. Defaults to the name of the member.</param>
		public StreamDeckButtonAttribute(string id = null) {
			Id = id;
		}

	}
}