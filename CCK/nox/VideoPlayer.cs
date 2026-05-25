/*#if HAS_NOX_FFMPEG
using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.FFmpeg;
using Nox.FFmpeg.Helpers;
using Nox.FFmpeg.Workers;
using Nox.VideoPlayer;
using UnityEngine;
using UnityEngine.Events;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.CCK.VideoPlayer.Nox {
	public class VideoPlayer : MonoBehaviour, IVideoPlayer, IVideoPlayerDetails, IVideoPlayerResolution, IVideoPlayerTexture {
		#region Fields

		public string playQuery;
		public Player player;

		private IResolve _currentPlaying;

		private void OnValidate() {
			Initializer.Initialize();

			// if url changed while playing, stop and play new
			if (!Player || !Player.IsPlaying)
				return;

			Player.Stop();
			if (!string.IsNullOrEmpty(playQuery))
				Play(playQuery);
		}

		#endregion Fields

		#region Properties

		// ReSharper disable Unity.PerformanceCriticalCodeInvocation
		public Player Player {
			get => player ??= GetComponent<Player>() ?? GetComponentInChildren<Player>();
			set => player = value;
		}

		#endregion Properties

		#region Unity Lifecycle

		private void Awake() {
			Initializer.Initialize();
			Player?.OnSeeked.AddListener(HandleSeek);
			Player?.OnLooping.AddListener(HandleLoop);
			Player?.OnError.AddListener(HandleError);
			Player?.OnMessage.AddListener(HandleMessage);
			Player?.OnPlayState.AddListener(HandleState);
			Player?.GetWorker<VideoWorker>()?.OnDisplay.AddListener(HandleTexture);
			Player?.GetWorker<VideoWorker>()?.OnResize.AddListener(HandleResolution);
			Player?.GetWorker<AudioWorker>()?.OnVolumeChange.AddListener(HandleVolume);
		}

		[ContextMenu("Play")]
		private void Start() {
			if (!string.IsNullOrEmpty(playQuery))
				Play(playQuery);
			VideoPlayerRegister.Register(this);
		}

		private void OnDestroy() {
			VideoPlayerRegister.UnRegister(this);
			Player?.OnSeeked.RemoveListener(HandleSeek);
			Player?.OnError.RemoveListener(HandleError);
			Player?.OnMessage.RemoveListener(HandleMessage);
			Player?.OnPlayState.RemoveListener(HandleState);
			Player?.OnLooping.RemoveListener(HandleLoop);
			Player?.GetWorker<VideoWorker>()?.OnDisplay.RemoveListener(HandleTexture);
			Player?.GetWorker<VideoWorker>()?.OnResize.RemoveListener(HandleResolution);
			Player?.GetWorker<AudioWorker>()?.OnVolumeChange.RemoveListener(HandleVolume);
		}

		#endregion Unity Lifecycle

		#region Resolving

		public UnityEvent<IVideoPlayer, IFetchOptions> OnResolving { get; } = new();

		public UnityEvent<IVideoPlayer, IFetchOptions, IResult[]> OnResolved { get; } = new();

		private IFetchOptions _currentFetchOptions;

		private async UniTask ResolveAsync(IFetchOptions fetchOptions) {
			_currentFetchOptions?.GetCancellation().Cancel();
			_currentFetchOptions = fetchOptions;
			Logger.LogDebug($"Resolving {_currentFetchOptions?.ToString() ?? "null"}");
			OnResolving.Invoke(this, fetchOptions);
			var results = await VideoPlayerResolver.Resolve(this, fetchOptions);
			OnResolve(fetchOptions, results);
		}

		public void OnResolve(IFetchOptions initial, IResult[] results) {
			if (_currentFetchOptions != initial)
				return; // Ignore if not the current fetch options

			foreach (var result in results) {
				if (result == null || !result.IsError())
					continue;
				Logger.LogWarning($"Error during fetch for {_currentFetchOptions?.ToString() ?? "null"}: {result.GetError()}");
			}

			var resolves = results.SelectMany(e => e.GetData()).ToArray();

			if (resolves.Length == 0) {
				Logger.LogWarning($"No data found for {_currentFetchOptions?.ToString() ?? "null"}");
				OnError.Invoke(this, new Exception("No data found"));
				return;
			}

			var first = resolves.FirstOrDefault();

			if (first == null) {
				Logger.LogWarning($"No results found for {_currentFetchOptions?.ToString() ?? "null"}");
				OnError.Invoke(this, new Exception("No results found"));
				return;
			}

			var tuple = first.FindQuality();

			if (tuple.Item1 == null) {
				Logger.LogWarning($"No compatible stream found for {_currentFetchOptions?.ToString() ?? "null"}");
				OnError.Invoke(this, new Exception("No compatible stream found"));
				return;
			}

			OnResolved.Invoke(this, initial, results);
			_currentPlaying = first;

			Play(tuple);
		}

		#endregion Resolving

		#region Playback

		private void PlayUrl(string vUrl, string aUrl = null, string sUrl = null) {
			try {
				if (aUrl == null && sUrl == null)
					Player.Play(vUrl);
				else if (sUrl == null)
					Player.Play(vUrl, aUrl);
				else
					Player.Play(vUrl, aUrl, sUrl);
			} catch (Exception e) {
				// Error already handled by Player.OnError, just prevent propagation
				Logger.LogError($"PlayUrl error: {e.Message}");
			}
		}

		private void Play((IFormat, IFormat) formats) {
			if (formats.Item1 is IAudioVideo av) {
				Play(av);
				return;
			}

			if (formats is { Item1: IVideo v, Item2: IAudio a }) {
				Play(v, a);
				return;
			}

			switch (formats.Item1) {
				case IVideo v2:
					Play(v2);
					break;
				case IAudio a2:
					Play(a2);
					break;
				default:
					Logger.LogError("Unsupported format type");
					OnError.Invoke(this, new Exception("Unsupported format type"));
					break;
			}
		}

		private void Play(IAudioVideo format) {
			if (format == null) {
				Logger.LogError("AudioVideo format is null");
				OnError.Invoke(this, new ArgumentNullException(nameof(format)));
				return;
			}

			var url = format.GetUrl();
			if (string.IsNullOrEmpty(url)) {
				Logger.LogError("AudioVideo URL is null or empty");
				OnError.Invoke(this, new Exception("AudioVideo URL is null or empty"));
				return;
			}

			PlayUrl(url);
		}

		private void Play(IVideo vFormat, IAudio aFormat) {
			if (vFormat == null || aFormat == null) {
				Logger.LogError($"Video or Audio format is null - Video: {vFormat != null}, Audio: {aFormat != null}");
				OnError.Invoke(this, new ArgumentNullException(vFormat == null ? nameof(vFormat) : nameof(aFormat)));
				return;
			}

			var videoUrl = vFormat.GetUrl();
			var audioUrl = aFormat.GetUrl();
			if (string.IsNullOrEmpty(videoUrl) || string.IsNullOrEmpty(audioUrl)) {
				Logger.LogError("Video or Audio URL is null or empty");
				OnError.Invoke(this, new Exception("Video or Audio URL is null or empty"));
				return;
			}

			try {
				Player.Play(videoUrl, audioUrl);
			} catch (Exception e) {
				// Error already handled by Player.OnError, just prevent propagation
				Logger.LogError($"Play(video, audio) error: {e.Message}");
			}
		}

		private void Play(IVideo vFormat) {
			if (vFormat == null) {
				Logger.LogError("Video format is null");
				OnError.Invoke(this, new ArgumentNullException(nameof(vFormat)));
				return;
			}

			var url = vFormat.GetUrl();
			if (string.IsNullOrEmpty(url)) {
				Logger.LogError("Video URL is null or empty");
				OnError.Invoke(this, new Exception("Video URL is null or empty"));
				return;
			}

			PlayUrl(url);
		}

		private void Play(IAudio aFormat) {
			if (aFormat == null) {
				Logger.LogError("Audio format is null");
				OnError.Invoke(this, new ArgumentNullException(nameof(aFormat)));
				return;
			}

			var url = aFormat.GetUrl();
			if (string.IsNullOrEmpty(url)) {
				Logger.LogError("Audio URL is null or empty");
				OnError.Invoke(this, new Exception("Audio URL is null or empty"));
				return;
			}

			PlayUrl(url);
		}

		#endregion Playback

		#region Actions

		public UnityEvent<IVideoPlayer> OnPlay { get; } = new();

		public UnityEvent<IVideoPlayer> OnPause { get; } = new();

		public UnityEvent<IVideoPlayer> OnResume { get; } = new();

		public UnityEvent<IVideoPlayer> OnStop { get; } = new();

		public UnityEvent<IVideoPlayer> OnBuffering { get; } = new();

		public UnityEvent<IVideoPlayer> OnStalled { get; } = new();

		public UnityEvent<IVideoPlayer> OnEnded { get; } = new();

		private void HandleState(PlayState state) {
			switch (state) {
				case PlayState.Playing:
					OnPlay.Invoke(this);
					break;
				case PlayState.Paused:
					OnPause.Invoke(this);
					break;
				case PlayState.Stopped:
					OnStop.Invoke(this);
					break;
				case PlayState.Buffering:
					OnBuffering.Invoke(this);
					break;
				case PlayState.Stalled:
					OnStalled.Invoke(this);
					break;
				case PlayState.Ended:
					OnEnded.Invoke(this);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(state), state, null);
			}
		}

		public bool IsPlaying
			=> Player.IsPlaying;

		public void Play(string query) {
			if (VideoPlayerResolver.IsMedia(this, query)) {
				PlayUrl(query);
				return;
			}
			ResolveAsync(new VideoFetchOptions { Query = query }).Forget();
		}

		[ContextMenu("Pause")]
		public void Pause()
			=> Player.Pause();

		[ContextMenu("Resume")]
		public void Resume()
			=> Player.Resume();

		[ContextMenu("Stop")]
		public void Stop()
			=> Player.Stop();

		#endregion Actions

		#region Metadata

		public string GetTitle()
			=> _currentPlaying?.GetTile();

		public string GetSubtitle()
			=> _currentPlaying?.GetSubtitle();

		#endregion Metadata

		#region Texture

		public UnityEvent<IVideoPlayer, Texture2D> OnTexture { get; } = new();

		private void HandleTexture(Texture2D texture)
			=> OnTexture.Invoke(this, texture);

		public Texture2D Texture
			=> Player.GetWorker<VideoWorker>()?.Texture;

		#endregion Texture

		#region Resolution

		public UnityEvent<IVideoPlayer, Vector2Int> OnResolution { get; } = new();

		private void HandleResolution(Vector2Int resolution)
			=> OnResolution.Invoke(this, resolution);

		public Vector2Int Resolution
			=> Player.GetWorker<VideoWorker>()?.Dimensions ?? Vector2Int.one;

		#endregion Resolution

		#region Volume

		public UnityEvent<IVideoPlayer, float> OnVolume { get; } = new();

		private void HandleVolume(float volume)
			=> OnVolume.Invoke(this, volume);

		public float Volume {
			get => Player.GetWorker<AudioWorker>()?.Volume ?? 0f;
			set => Player.GetWorker<AudioWorker>().Volume = value;
		}

		#endregion Volume

		#region Time

		public double Time {
			get => Player.PlaybackTime;
			set => Player.Seek(value);
		}

		public double Duration
			=> Player.Length;

		public double Progress
			=> Duration > 0 ? Time / Duration : 0;

		private void HandleSeek(double time)
			=> OnSeek.Invoke(this, time);

		public UnityEvent<IVideoPlayer, double> OnSeek { get; } = new();

		#endregion Time

		#region Loop

		public bool Loop {
			get => Player.IsLooping;
			set => Player.IsLooping = value;
		}

		private void HandleLoop(bool loop)
			=> OnLoop.Invoke(this, loop);

		public UnityEvent<IVideoPlayer, bool> OnLoop { get; } = new();

		#endregion Loop

		#region Debug

		public UnityEvent<IVideoPlayer, string> OnMessage { get; } = new();

		private void HandleMessage(LogType level, string message) {
			OnMessage.Invoke(this, message);
			switch (level) {
				case LogType.Log:
					Logger.Log(message);
					break;
				case LogType.Error:
				case LogType.Exception:
					Logger.LogError(message);
					break;
				case LogType.Warning:
					Logger.LogWarning(message);
					break;
				default:
					Logger.LogDebug(message);
					break;
			}
		}

		public UnityEvent<IVideoPlayer, Exception> OnError { get; } = new();

		private void HandleError(Exception exception) {
			OnError.Invoke(this, exception);
			Logger.LogError(new Exception($"Error in VideoPlayer", exception));
		}

		public override string ToString()
			=> $"{GetType().Name}[Playing={IsPlaying}, Time={Time}/{Duration} ({Progress:P2}), Resolution={Resolution.x}x{Resolution.y}, Volume={Volume:P2}, Loop={Loop}]";

		#endregion Debug
	}
}
#endif*/