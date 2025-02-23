namespace F10.StreamDeckIntegration {
#if UNITY_EDITOR
	[UnityEditor.InitializeOnLoad]
#endif
	internal static class StreamDeckHelper {

		static StreamDeckHelper() {
#if UNITY_EDITOR
#if UNITY_2017_1_OR_NEWER
			UnityEditor.EditorApplication.playModeStateChanged += PlayModeStateChanged;
			UnityEditor.EditorApplication.pauseStateChanged += PauseModeStateChanged;
#else
            UnityEditor.EditorApplication.playmodeStateChanged += PlayModeStateChanged;
#endif
#endif
		}

		internal static void SwitchPlayMode() {
#if UNITY_EDITOR
			if (UnityEditor.EditorApplication.isPlaying) {
#if UNITY_2019_1_OR_NEWER
				UnityEditor.EditorApplication.ExitPlaymode();
#else
				UnityEditor.EditorApplication.isPlaying = false;
#endif
			} else {
#if UNITY_2019_1_OR_NEWER
				UnityEditor.EditorApplication.EnterPlaymode();
#else
				UnityEditor.EditorApplication.isPlaying = true;
#endif
			}
#endif
		}

		internal static void SwitchPauseMode() {
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPaused = !UnityEditor.EditorApplication.isPaused;
#endif
		}

		internal static void ExecuteMenu(string menuPath) {
#if UNITY_EDITOR
			UnityEditor.EditorApplication.ExecuteMenuItem(menuPath);
#endif
		}

#if UNITY_EDITOR
#if UNITY_2017_1_OR_NEWER
		private static void PlayModeStateChanged(UnityEditor.PlayModeStateChange playMode) {
#else
        private static void PlayModeStateChanged() {
#endif
            int state;

#if UNITY_2017_1_OR_NEWER
			switch (playMode) {
				case UnityEditor.PlayModeStateChange.ExitingEditMode:
					state = 1;
					break;
				case UnityEditor.PlayModeStateChange.ExitingPlayMode:
					state = 0;
					break;
				default:
					return;
			}
#else
			state = UnityEditor.EditorApplication.isPlaying ? 1 : 0;
#endif

			var json = "{\"event\":\"playModeStateChanged\",\"payload\":{\"state\":" + state + "}}";
			StreamDeckSocket.Send(json);
		}

#if UNITY_2017_1_OR_NEWER
		private static void PauseModeStateChanged(UnityEditor.PauseState pauseState) {
			int state;

			switch (pauseState) {
				case UnityEditor.PauseState.Paused:
					state = 1;
					break;
				case UnityEditor.PauseState.Unpaused:
					state = 0;
					break;
				default:
					return;
			}

			var json = "{\"event\":\"pauseModeStateChanged\",\"payload\":{\"state\":" + state + "}}";
			StreamDeckSocket.Send(json);
		}
#endif
#endif
		internal static void GetConnectedDevices() {
			const string json = "{\"event\":\"getDevices\"}";
			StreamDeckSocket.Send(json, true);
		}

	}
}