using Nox.VideoPlayer;
using UnityEngine;

namespace api.nox.videoplayer {
	public static class VideoPlayerExtensions {
		public static GameObject GetGameObject(this IVideoPlayer self)
			=> self is MonoBehaviour mb ? mb.gameObject : null;

		public static int GetId(this IVideoPlayer self)
			=> self.GetGameObject().GetEntityId().GetHashCode();
	}
}