using System;
using JetBrains.Annotations;
using UnityEngine;

namespace F10.StreamDeckIntegration {
	/// <summary>
	/// Handles changes to Stream Deck actions, like title and icon control.
	/// </summary>
	[PublicAPI]
	public static class StreamDeckSettings {

		/// <summary>
		/// Set a new title on the Stream Deck action linked to the passed member.
		/// <br/>
		/// If the target action has a custom title (manually set title on the Stream Deck software) changes won't be visible.
		/// </summary>
		/// <param name="title">Text to set as the action's title.</param>
		/// <param name="id">Member ID of the targeted action.</param>
		/// <param name="groupId">Optional.<br/>Group ID of the targeted action. Defaults to null for actions without groups.</param>
		/// <param name="immediate">Optional.<br/>Allows the setting to be sent immediately, instead of being added to the queue. Defaults to false.</param>
		public static void SetButtonTitle([NotNull] string title, [NotNull] string id, string groupId = null, bool immediate = false) {
			var buttonData = StreamDeck.FindButtonById(groupId, id);
			if (buttonData == null) {
				Log.Warning("There isn't any registered button with group ID " + groupId + " and / or " + id + "!");
				return;
			}

            var json = "{\"event\":\"setTitle\",\"payload\":{\"title\":\"" + title + "\",\"group-id\":\"" + (groupId ?? string.Empty) + "\",\"id\":\"" + id + "\"}}";
            StreamDeckSocket.Send(json, immediate);
		}

		/// <summary>
		/// Set a new image / icon on the Stream Deck action linked to the passed member.
		/// <br/>
		/// If the target action has a custom icon (manually set icon on the Stream Deck software) changes won't be visible.
		/// </summary>
		/// <param name="image">Texture2D to set as the action's image / action.</param>
		/// <param name="id">Member ID of the targeted action.</param>
		/// <param name="groupId">Optional.<br/>Group ID of the targeted action. Defaults to null for actions without groups.</param>
		/// <param name="immediate">Optional.<br/>Allows the setting to be sent immediately, instead of being added to the queue. Defaults to false.</param>
		public static void SetButtonImage([NotNull] Texture2D image, [NotNull] string id, string groupId = null, bool immediate = false) {
			var buttonData = StreamDeck.FindButtonById(groupId, id);
			if (buttonData == null) {
				Log.Warning("There isn't any registered button with group ID " + groupId + " and / or " + id + "!");
				return;
			}

			var base64 = Convert.ToBase64String(image.EncodeToPNG());
			var encodedImage = "data:image/png;base64," + base64;

            var json = "{\"event\":\"setImage\",\"payload\":{\"image\":\"" + encodedImage + "\",\"group-id\":\"" + (groupId ?? string.Empty) + "\",\"id\":\"" + id + "\"}}";
            StreamDeckSocket.Send(json, immediate);
		}
		
		/// <summary>
		/// Set the value of the parameter on the Stream Deck action linked to the passed member.
		/// </summary>
		/// <param name="value">A supported type value to set on the targeted action.</param>
		/// <param name="id">Member ID of the targeted action.</param>
		/// <param name="groupId">Optional.<br/>Group ID of the targeted action. Defaults to null for actions without groups.</param>
		/// <param name="immediate">Optional.<br/>Allows the setting to be sent immediately, instead of being added to the queue. Defaults to false.</param>
		public static void SetButtonValue([NotNull] object value, [NotNull] string id, string groupId = null, bool immediate = false) {
            var json = "{\"event\":\"setValue\",\"payload\":{\"value\":\"" + value + "\",\"group-id\":\"" + (groupId ?? string.Empty) + "\",\"id\":\"" + id + "\"}}";
            StreamDeckSocket.Send(json, immediate);
		}
		
		// TODO: GetIdByCoordinate(string deviceId, int page, Vector2Int coordinate)

		/// <summary>
		/// Configure the Stream Deck scripted action linked by the given ID.
		/// </summary>
		/// <param name="id">Member ID of the targeted action.</param>
		/// <param name="callback">Method to invoke on action trigger.</param>
		/// <param name="title">Text to set as the action's title.</param>
		/// <param name="image">Texture2D to set as the action's image / action.</param>
		/// <param name="immediate">Optional.<br/>Allows the setting to be sent immediately, instead of being added to the queue. Defaults to false.</param>
		public static void SetScriptedButton([NotNull] string id, [NotNull] Action callback, string title = null, Texture2D image = null, bool immediate = false) {
			var buttonData = StreamDeck.FindButtonById(StreamDeck.MainGroup, id);
			if (buttonData != null) {
				buttonData.SetCallback(callback);
			} else {
				StreamDeck.AddAction(callback.Target, callback.Method, StreamDeck.MainGroup, id);
			}

			if (title != null) {
				SetButtonTitle(title, id, null, immediate);
			}
			if (image != null) {
				SetButtonImage(image, id, null, immediate);
			}
		}
		
		/// <summary>
		/// Remove the configuration of the Stream Deck scripted action linked by the given ID.
		/// </summary>
		/// <param name="id">Member ID of the targeted action.</param>
		public static void RemoveScriptedButton([NotNull] string id) {
			StreamDeck.RemoveAction(StreamDeck.MainGroup, id);
		}
	}
}