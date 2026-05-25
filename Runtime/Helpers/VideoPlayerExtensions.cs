using Nox.VideoPlayer;
using UnityEngine;

namespace Nox.VideoPlayer.Runtime.Helpers {
	public static class VideoPlayerExtensions {
		public static GameObject GetGameObject(this IVideoPlayer self)
			=> self is MonoBehaviour mb ? mb.gameObject : null;

		public static int GetId(this IVideoPlayer self)
			=> self.GetGameObject().GetEntityId().GetHashCode();
	}
}