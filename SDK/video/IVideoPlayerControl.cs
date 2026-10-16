namespace Nox.VideoPlayer {
	/// <summary>
	/// Permission surface of a video player: whether the local user is allowed to change
	/// the current media (set a new url / search a new video). Defined by the world or a
	/// script.
	/// </summary>
	public interface IVideoPlayerControl {
		/// <summary>
		/// When true, the local user cannot change the video: no new url, no search.
		/// </summary>
		bool IsReadonly { get; }
	}

	/// <summary>
	/// Source surface of a video player: the last launched url, and the ability to open
	/// an already resolved media (used by the search page to play a selected result).
	/// </summary>
	public interface IVideoPlayerSource {
		/// <summary>
		/// The last launched url. Setting it plays the new media.
		/// </summary>
		string Url { get; set; }

		/// <summary>
		/// Play an already resolved media at the best compatible quality.
		/// </summary>
		void Play(IResolve resolve);
	}
}
