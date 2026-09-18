using UnityEngine;
using UnityEngine.Events;

namespace Nox.VideoPlayer {
	/// <summary>How the current media is delivered.</summary>
	public enum PlayType {
		/// <summary>Nothing is being played (no media opened).</summary>
		None = 0,

		/// <summary>A media with a known duration: it can be seeked (file, VOD).</summary>
		Media,

		/// <summary>A stream without a known duration: it cannot be seeked (live).</summary>
		Stream,
	}

	public interface IVideoPlayerDetails {

		/// <summary>
		/// Event invoked when metadata changes.
		/// </summary>
		public UnityEvent<IVideoPlayer> OnMetadata { get; }

		/// <summary>
		/// The title of the video (if available).
		/// </summary>
		public string Title { get; }

		/// <summary>
		/// The description of the video (if available).
		/// </summary>
		public string Subtitle { get; }

		/// <summary>
		/// The thumbnail of the video (if available).
		/// </summary>
		public Texture2D Thumbnail { get; }

		/// <summary>
		/// How the current media is delivered: <see cref="PlayType.Media"/> when it has
		/// a known duration, <see cref="PlayType.Stream"/> when it has none.
		/// </summary>
		public PlayType Type { get; }

		/// <summary>
		/// Whether the current media is a stream (<see cref="PlayType.Stream"/>): it has
		/// no duration, therefore it cannot be seeked (live).
		/// </summary>
		public bool IsStream { get; }
	}
}