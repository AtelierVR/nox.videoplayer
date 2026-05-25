using System.Collections.Generic;
using System.Linq;
using Nox.CCK.Utils;
using Nox.CCK.VideoPlayer;
using Nox.VideoPlayer;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace api.nox.videoplayer {
	public static class VideoPlayerManager {
		public static readonly List<IVideoPlayer> VideoPlayers = new();

		public static readonly UnityEvent<IVideoPlayer> OnRegistered = new();
		public static readonly UnityEvent<IVideoPlayer> OnUnRegistered = new();

		public static void Listen() {
			VideoPlayerRegister.OnRegister.AddListener(Register);
			VideoPlayerRegister.OnUnRegister.AddListener(UnRegister);
			SceneManager.sceneUnloaded += OnSceneUnloaded;
			SceneManager.sceneLoaded   += OnSceneLoaded;
			for (var i = 0; i < SceneManager.sceneCount; i++)
				OnSceneLoaded(SceneManager.GetSceneAt(i), LoadSceneMode.Single);
		}

		public static void UnListen() {
			VideoPlayerRegister.OnRegister.RemoveListener(Register);
			VideoPlayerRegister.OnUnRegister.RemoveListener(UnRegister);
			SceneManager.sceneUnloaded -= OnSceneUnloaded;
			SceneManager.sceneLoaded   -= OnSceneLoaded;
			for (var i = 0; i < SceneManager.sceneCount; i++)
				OnSceneUnloaded(SceneManager.GetSceneAt(i));
		}

		private static void OnSceneUnloaded(Scene scene) {
			foreach (var player in VideoPlayers.ToArray())
				if (player.GetGameObject().scene == scene)
					UnRegister(player);
		}

		private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
			var components = scene.GetComponentsInChildren<IVideoPlayer>(true);
			Logger.LogDebug($"Found {components.Length} video players in scene {scene.name}");
			foreach (var player in components)
				Register(player);
		}

		private static void Register(IVideoPlayer player) {
			if (player == null)
				return;
			if (VideoPlayers.Contains(player))
				return;
			VideoPlayers.Add(player);
			Logger.LogDebug($"Registered video player {player}");
			Main.Instance.CoreAPI.EventAPI.Emit("video_player_registered", player);
			OnRegistered.Invoke(player);
		}

		private static void UnRegister(IVideoPlayer player) {
			if (player == null)
				return;
			if (!VideoPlayers.Contains(player))
				return;
			VideoPlayers.Remove(player);
			Logger.LogDebug($"Unregistered video player {player}");
			Main.Instance.CoreAPI.EventAPI.Emit("video_player_unregistered", player);
			OnUnRegistered.Invoke(player);
		}
		
		public static IEnumerable<IVideoPlayer> ActiveVideoPlayers
			=> VideoPlayers.Where(p => p.GetGameObject().activeInHierarchy);
	}
}