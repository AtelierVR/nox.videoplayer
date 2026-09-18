using UnityEngine;
using UnityEngine.Events;

namespace Nox.VideoPlayer {
	public interface IVideoPlayerAudio {
		/// <summary>
		/// The current audio clip of the video player.
		/// </summary>
		public AudioClip Clip { get; }

		/// <summary>
		/// Event invoked when a new audio clip is available.
		/// </summary>
		public UnityEvent<IVideoPlayer, AudioClip> OnClip { get; }

		/// <summary>
		/// The volume of the video player (0 to 1).
		/// </summary>
		public float Volume { get; set; }

		/// <summary>
		/// Event invoked when the volume changes.
		/// </summary>
		public UnityEvent<IVideoPlayer, float> OnVolume { get; }

		/// <summary>
		/// Whether the audio is muted.
		/// </summary>
		public bool Muted { get; set; }

		/// <summary>
		/// Event invoked when the mute state changes.
		/// </summary>
		public UnityEvent<IVideoPlayer, bool> OnMute { get; }

		/// <summary>
		/// The audio tracks of the current media, and the one being decoded.
		/// </summary>
		public ITrack AudioTracks { get; }
	}
}