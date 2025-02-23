using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using F10.StreamDeckIntegration.Attributes;
using JetBrains.Annotations;
using UnityEngine;

namespace F10.StreamDeckIntegration {
    /// <summary>
    /// Handles adding and removing members that can be executed by Stream Deck.
    /// </summary>
#if UNITY_2018_1_OR_NEWER
	[ExecuteAlways]
#else
    [ExecuteInEditMode]
#endif
    public static class StreamDeck {
		internal const string MainGroup = "StreamDeck_Main";

		internal static readonly Dictionary<string, List<StreamDeckButtonData>> Groups = new Dictionary<string, List<StreamDeckButtonData>>();
		
		internal static readonly List<StreamDeckButtonData> Dials = new List<StreamDeckButtonData>();
		
		static StreamDeck() {
			if (!Groups.ContainsKey(MainGroup)) {
				Groups.Add(MainGroup, new List<StreamDeckButtonData>());
			}
		}

		/// <summary>
		/// Adds the given type as a static member to the list of available <see cref="StreamDeckButtonAttribute"/> and <see cref="StreamDeckGroupAttribute"/>.
		/// </summary>
		/// <param name="type">Type of the static member to add.</param>
		public static void AddStatic(Type type) {
			Internal_Add(type, null);
		}

		/// <summary>
		/// Adds the given object member instance to the list of available <see cref="StreamDeckButtonAttribute"/> and <see cref="StreamDeckGroupAttribute"/>.
		/// </summary>
		/// <param name="obj">Boxed member to add.</param>
		public static void Add(object obj) {
			var type = obj.GetType();
			Internal_Add(type, obj);
		}

		/// <summary>
		/// Removes the given object member instance to the list of available <see cref="StreamDeckButtonAttribute"/> and <see cref="StreamDeckGroupAttribute"/>.
		/// </summary>
		/// <param name="obj">Boxed member to remove.</param>
		public static void Remove(object obj) {
			var type = obj.GetType();

			if (Attribute.IsDefined(type, typeof(StreamDeckGroupAttribute), false)) {
                var attribute = Attribute.GetCustomAttribute(type, typeof(StreamDeckGroupAttribute), false) as StreamDeckGroupAttribute;

				// ReSharper disable once PossibleNullReferenceException
				var id = attribute.Id ?? type.Name;
				Groups.Remove(id);
			}

			var members = type.GetMembers(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
			foreach (var member in members) {
				if (Attribute.IsDefined(member, typeof(StreamDeckButtonAttribute), false)) {
                    var attribute = Attribute.GetCustomAttribute(member, typeof(StreamDeckButtonAttribute), false) as StreamDeckButtonAttribute;
					// ReSharper disable once PossibleNullReferenceException
					RemoveAction(MainGroup, attribute.Id ?? member.Name);
				}
				
				if (Attribute.IsDefined(member, typeof(StreamDeckDialAttribute), false)) {
                    var attribute = Attribute.GetCustomAttribute(member, typeof(StreamDeckDialAttribute), false) as StreamDeckDialAttribute;

                    // ReSharper disable once PossibleNullReferenceException
                    RemoveDial(attribute.Id ?? member.Name);
				}
			}
		}

		[CanBeNull]
		internal static StreamDeckButtonData FindButtonById([CanBeNull]string groupId, [NotNull]string memberId) {
			if (groupId == null || string.IsNullOrEmpty(groupId.Trim())) {
				groupId = MainGroup;
			}

			List<StreamDeckButtonData> group;
			if (!Groups.TryGetValue(groupId, out group)) {
				Log.Debug("Unable to find the group with ID \"" +groupId + "\". The instance may not have been registered in time.");
				return null;
			}

			StreamDeckButtonData buttonData = group.FirstOrDefault(button => button.Id == memberId);
			if (buttonData != null) return buttonData;
			
			Log.Debug("Unable to find the button with ID \"" + memberId + "\" inside the group with ID \"" + groupId + "\". The instance may not have been registered in time.");
			return null;
		}

		internal static StreamDeckButtonData AddAction(object target, [NotNull] MemberInfo member, string group, string id = null) {
			var buttonData = new StreamDeckButtonData(target, member, id);

			if (!Groups.ContainsKey(group)) {
				Groups.Add(group, new List<StreamDeckButtonData>());
			}
			Groups[group].Add(buttonData);

			return buttonData;
		}
		
		internal static void RemoveAction(string group, string id = null) {
			if (!Groups.ContainsKey(group)) return;

			var index = Groups[group].FindIndex(x => x.Id == id);
			if (index >= 0) {
				Groups[MainGroup].RemoveAt(index);
			}
		}
		
		internal static StreamDeckButtonData AddDial(object target, [NotNull] MemberInfo member, string id) {
			var dialData = new StreamDeckButtonData(target, member, id);
			Dials.Add(dialData);

			return dialData;
		}
		
		internal static void RemoveDial(string id) {
			var index = Dials.FindIndex(x => x.Id == id);
			if (index >= 0) {
				Dials.RemoveAt(index);
			}
		}
		
		private static void Internal_Add(Type type, object obj) {
			var members = type.GetMembers(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

			// [StreamDeckGroup]
			if (Attribute.IsDefined(type, typeof(StreamDeckGroupAttribute), false)) {
                var attribute = Attribute.GetCustomAttribute(type, typeof(StreamDeckGroupAttribute), false) as StreamDeckGroupAttribute;

                // ReSharper disable once PossibleNullReferenceException
                var groupId = attribute.Id ?? type.Name;

				if (Groups.ContainsKey(groupId)) {
					Log.Warning("The group ID \"" + groupId + "\" is already defined and added to active groups!\nEach StreamDeckGroup must have a unique ID. Custom ID's can be added as part of the attribute.\nThis group will be ignored.");
					return;
				}

				foreach (var member in members) {
					AddAction(obj, member, groupId);
				}
				
				return;
			}

			// [StreamDeckButton], [StreamDeckDial]
			foreach (var member in members) {
				if (Attribute.IsDefined(member, typeof(StreamDeckButtonAttribute), false)) {

					var attribute = Attribute.GetCustomAttribute(member, typeof(StreamDeckButtonAttribute), false) as StreamDeckButtonAttribute;
					// ReSharper disable once PossibleNullReferenceException
					var id = attribute.Id ?? member.Name;

					if (Groups[MainGroup].Any(x => x.Id == id)) {
						Log.Warning("The ID \"" + id + "\" is already defined and added to active buttons!\nEach StreamDeckButton must have a unique ID. Custom ID's can be added as part of the attribute.\nThis button will be ignored.");
						return;
					}

					AddAction(obj, member, MainGroup, id);
				}
				
				if (Attribute.IsDefined(member, typeof(StreamDeckDialAttribute), false)) {
                    var attribute = Attribute.GetCustomAttribute(member, typeof(StreamDeckDialAttribute), false) as StreamDeckDialAttribute;
					// ReSharper disable once PossibleNullReferenceException
					var id = attribute.Id ?? member.Name;

					AddDial(obj, member, id);
				}
			}
		}
	}
}