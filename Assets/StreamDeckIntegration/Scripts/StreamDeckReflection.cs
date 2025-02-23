using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using F10.StreamDeckIntegration.Json;
using JetBrains.Annotations;

namespace F10.StreamDeckIntegration {
	internal static class StreamDeckReflection {

		[CanBeNull]
#if UNITY_2017_1_OR_NEWER
        private static string GetGroup(IReadOnlyDictionary<string, JsonData> data) {
#else
        private static string GetGroup(IDictionary<string, JsonData> data) {
#endif
			var isGroup = data["settings"].AsData.ContainsKey("group") && data["settings"].AsData["group"].AsBool;

			var groupId = isGroup ? data["settings"].AsData["group-id"].AsString : StreamDeck.MainGroup;
			if (StreamDeck.Groups.ContainsKey(groupId)) return groupId;

			Log.Warning("No group ID \"" + groupId + "\" registered");
			return null;
		}

		[CanBeNull]
		private static StreamDeckButtonData GetButtonData(string groupId, string memberId) {
			var buttonData = StreamDeck.Groups[groupId].FirstOrDefault(x => x.Id == memberId);
			if (buttonData != null) return buttonData;

			// ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
			if (groupId == StreamDeck.MainGroup) {
				Log.Warning("No member ID \"" + memberId + "\" found");
			} else {
				Log.Warning("No member ID \"" + memberId + "\" found in \"" + groupId + "\"");
			}

			return null;
		}

#if UNITY_2017_1_OR_NEWER
        internal static void InvokeMethod(IReadOnlyDictionary<string, JsonData> data) {
#else
        internal static void InvokeMethod(IDictionary<string, JsonData> data) {
#endif

            if (data["settings"].AsData.Keys.Count <= 0) {
				Log.Warning("The pressed action has no configuration!");
				return;
			}
			
			var groupId = GetGroup(data);
			if (groupId == null) return;

			var memberId = data["settings"].AsData["id"].AsString;
			var buttonData = GetButtonData(groupId, memberId);
			if (buttonData == null) return;

			var info = buttonData.Member as MethodInfo;
			if (info == null) {
				Log.Error("The ID \"" + memberId + "\" is not a method!");
				return;
			}

			// Check if method has parameters
			var parameters = info.GetParameters();
			var hasParam = data["settings"].AsData.ContainsKey("param") && data["settings"].AsData["param"].AsBool;

			if (parameters.Length <= 0 || !hasParam) {
				info.Invoke(buttonData.Target, null);
			} else {
				var paramValue = data["settings"].AsData["value"];
				var paramType = data["settings"].AsData["type"].AsString;

				info.Invoke(buttonData.Target, new[] {paramValue.AsCastedObject(paramType)});
			}
		}

#if UNITY_2017_1_OR_NEWER
        internal static void SetFieldOrProperty(IReadOnlyDictionary<string, JsonData> data) {
#else
        internal static void SetFieldOrProperty(IDictionary<string, JsonData> data) {
#endif
            if (data["settings"].AsData.Keys.Count <= 0) {
				Log.Warning("The pressed action has no configuration!");
				return;
			}
			
			var groupId = GetGroup(data);
			if (groupId == null) return;

			var memberId = data["settings"].AsData["id"].AsString;
			var buttonData = GetButtonData(groupId, memberId);
			if (buttonData == null) return;

			var infoProperty = buttonData.Member as PropertyInfo;
			var infoField = buttonData.Member as FieldInfo;
			if (infoProperty == null && infoField == null) {
				Log.Error("The ID \"" + memberId + "\" is not a property nor a field!");
				return;
			}

			var value = data["settings"].AsData["value"];
			var type = data["settings"].AsData["type"].AsString;

			if (infoProperty != null) {
				infoProperty.SetValue(buttonData.Target, value.AsCastedObject(type), null);
			}

            if (infoField != null) {
                infoField.SetValue(buttonData.Target, value.AsCastedObject(type));
            }
		}

#if UNITY_2017_1_OR_NEWER
        internal static void InvokeDial(IReadOnlyDictionary<string, JsonData> data) {
#else
        internal static void InvokeDial(IDictionary<string, JsonData> data) {
#endif
            if (data["settings"].AsData.Keys.Count <= 0) {
				Log.Warning("The pressed action has no configuration!");
				return;
			}

			var memberId = data["settings"].AsData["id"].AsString;
			
			var dialData = StreamDeck.Dials.FirstOrDefault(x => x.Id == memberId);
			if (dialData == null) {
				Log.Warning("No member ID \"" + memberId + "\" found");
				return;
			}
			
			var info = dialData.Member as MethodInfo;
			if (info == null) {
				Log.Error("The ID \"" + memberId + "\" is not a method!");
				return;
			}
			
			// 0 => Off, press
			// 1 => On, press
			// 2 => Off, rotate
			// 3 => On, rotate
			info.Invoke(dialData.Target, new object[] {
				data["state"].AsInt <= 1 ? StreamDeckDialAction.Press : StreamDeckDialAction.Rotation,
				data["state"].AsInt %2 != 0,
				data["settings"].AsData["value"].AsDouble
			});
		}

	}
}