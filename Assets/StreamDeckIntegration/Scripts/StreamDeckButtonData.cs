using System;
using System.Reflection;
using JetBrains.Annotations;

namespace F10.StreamDeckIntegration {
	internal class StreamDeckButtonData {

		public string Id { get; private set; }

		public object Target { get; private set; }

		public MemberInfo Member { get; private set; }

		public StreamDeckButtonData(object target, [NotNull] MemberInfo member, string id = null) {
			if (id == null) {
				id = member.Name;
			}

			Id = id;
			Target = target;
			Member = member;
		}

		public void SetCallback(Action action) {
			Target = action.Target;
			Member = action.Method;
		}

	}
}