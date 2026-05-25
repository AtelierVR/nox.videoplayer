using UnityEngine;
using UnityEngine.Events;

namespace Nox.VideoPlayer {
	public interface IVideoPlayerResolution {
		/// <summary>
		/// Event invoked when the resolution changes.
		/// </summary>
		public UnityEvent<IVideoPlayer, Vector2Int> OnResolution { get; }

		/// <summary>
		/// The current resolution of the video player.
		/// </summary>
		public Vector2Int Resolution { get; }
	}
}