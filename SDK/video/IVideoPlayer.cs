using UnityEngine.Events;
using LogType = Nox.CCK.Utils.LogType;

namespace Nox.VideoPlayer {
	public interface IVideoPlayer {
		#region Debug

		/// <summary>
		/// Event invoked for debug messages.
		/// </summary>
		public UnityEvent<IVideoPlayer, LogType, string> OnMessage { get; }

		#endregion Debug

		#region Time

		/// <summary>
		/// The current playback time of the video in seconds.
		/// </summary>
		public double Time { get; set; }

		/// <summary>
		/// The total duration of the video in seconds.
		/// </summary>
		public double Duration { get; }

		/// <summary>
		/// The current progress of the video as a value between 0.0 and 1.0.
		/// </summary>
		public double Progress { get; }

		/// <summary>
		/// Event invoked when Time is changed (seeked).
		/// </summary>
		public UnityEvent<IVideoPlayer, double> OnSeek { get; }

		#endregion Time

		#region Loop

		/// <summary>
		/// Whether the video should loop when it reaches the end.
		/// </summary>
		public bool Loop { get; set; }

		/// <summary>
		/// Event invoked when the looping state changes.
		/// </summary>
		public UnityEvent<IVideoPlayer, bool> OnLoop { get; }

		#endregion Loop

		#region Stream

		/// <summary>
		/// Event invoked when the player switches to another stream: a new media has been
		/// opened, or another track has been selected. Use it to refresh everything that
		/// depends on the stream (duration, resolution, tracks…) once, instead of polling
		/// it every frame.
		/// </summary>
		public UnityEvent<IVideoPlayer> OnStream { get; }

		#endregion Stream

		#region Actions
		
		/// <summary>
		/// The current state of the video player.
		/// </summary>
		public State State { get; }

		/// <summary>
		/// Event invoked when the video player state changes.
		/// </summary>
		public UnityEvent<IVideoPlayer, State> OnState { get; }

		/// <summary>
		/// Play a video from the given query (URL or file path).
		/// </summary>
		/// <param name="query"></param>
		public void Play(string query);

		/// <summary>
		/// Pause the currently playing video.
		/// </summary>
		public void Pause();

		/// <summary>
		/// Resume the currently paused video.
		/// </summary>
		public void Resume();

		/// <summary>
		/// Stop the currently playing video.
		/// </summary>
		public void Stop();

		#endregion Actions
	}
}