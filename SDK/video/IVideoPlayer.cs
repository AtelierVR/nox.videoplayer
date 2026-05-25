using System;
using UnityEngine;
using UnityEngine.Events;

namespace Nox.VideoPlayer {
	public interface IVideoPlayer {
		#region Debug

		/// <summary>
		/// Event invoked when an error occurs.
		/// </summary>
		public UnityEvent<IVideoPlayer, Exception> OnError { get; }

		/// <summary>
		/// Event invoked for debug messages.
		/// </summary>
		public UnityEvent<IVideoPlayer, string> OnMessage { get; }

		#endregion Debug

		#region Volume

		/// <summary>
		/// The current volume of the video player (0.0 to 1.0).
		/// </summary>
		public float Volume { get; set; }

		/// <summary>
		/// Event invoked when the volume changes.
		/// </summary>
		public UnityEvent<IVideoPlayer, float> OnVolume { get; }

		#endregion Volume

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

		#region Actions
		
		/// <summary>
		/// Whether a video is currently playing.
		/// </summary>
		public bool IsPlaying { get; }

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

		/// <summary>
		/// Event invoked when a video starts playing.
		/// </summary>
		public UnityEvent<IVideoPlayer> OnPlay { get; }

		/// <summary>
		/// Event invoked when a video is paused.
		/// </summary>
		public UnityEvent<IVideoPlayer> OnPause { get; }

		/// <summary>
		/// Event invoked when a video is resumed.
		/// </summary>
		public UnityEvent<IVideoPlayer> OnResume { get; }

		/// <summary>
		/// Event invoked when a video is stopped.
		/// </summary>
		public UnityEvent<IVideoPlayer> OnStop { get; }

		#endregion Actions
	}
}