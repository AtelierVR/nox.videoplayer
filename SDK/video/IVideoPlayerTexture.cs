using UnityEngine;
using UnityEngine.Events;

namespace Nox.VideoPlayer {
	public interface IVideoPlayerTexture {
		/// <summary>
		/// Event invoked when a new texture is available.
		/// </summary>
		public UnityEvent<IVideoPlayer, Texture2D> OnTexture { get; }

		/// <summary>
		/// The current texture of the video player.
		/// </summary>
		public Texture2D Texture { get; }
	}
}