using UnityEngine;
using UnityEngine.Events;

namespace Nox.VideoPlayer {
	public interface IVideoPlayerVideo {
		/// <summary>
		/// Event invoked when a new texture is available.
		/// </summary>
		public UnityEvent<IVideoPlayer, Texture2D> OnTexture { get; }

		/// <summary>
		/// The current texture of the video player.
		/// </summary>
		public Texture2D Texture { get; }

		/// <summary>
		/// The video tracks of the current media, and the one being decoded.
		/// </summary>
		public ITrack VideoTracks { get; }
	}
}