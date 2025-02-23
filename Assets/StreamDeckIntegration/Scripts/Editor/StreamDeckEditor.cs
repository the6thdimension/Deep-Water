using UnityEditor;

namespace F10.StreamDeckIntegration.Editor {
	/// <summary>
	/// Keeps <see cref="StreamDeckSocket"/> updated when the integration is running on the editor.
	/// </summary>
	[InitializeOnLoad]
	internal static class StreamDeckEditor {

		static StreamDeckEditor() {
			EditorApplication.update += StreamDeckSocket.OnUpdate;
			Internal_Connect();
		}

		[MenuItem("Stream Deck/Connect")]
		private static void Internal_Connect() {
			StreamDeckSocket.Connect();
		}
		
		[MenuItem("Stream Deck/Disconnect", true)]
		private static bool Internal_Disconnect_Validate() {
			return StreamDeckSocket.IsConnected;
		}
		
		[MenuItem("Stream Deck/Disconnect")]
		private static void Internal_Disconnect() {
			StreamDeckSocket.Disconnect();
		}

	}
}