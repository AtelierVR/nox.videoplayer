/*#if HAS_UNITY_VIDEOPLAYER
using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.VideoPlayer;
using UnityEngine;
using UnityEngine.Events;
using Logger = Nox.CCK.Utils.Logger;
using UnityVideoPlayer = UnityEngine.Video.VideoPlayer;

namespace Nox.CCK.VideoPlayer.Unity {
	public class VideoPlayer : MonoBehaviour, IVideoPlayer, IVideoPlayerResolver, IVideoPlayerDetails, IVideoPlayerResolution {
		#region Fields

		public string           playQuery;
		public UnityVideoPlayer videoPlayer;
		public AudioSource      audioSource;

		private       string   _currentUrl;
		private       bool     _isReconnecting;
		private       int      _reconnectAttempts;
		private const int      MaxReconnectAttempts = 3;
		private const float    ReconnectDelay = 2;
		private       double   _lastProgress = -1;
		private       bool     _lastPlayingStatus;
		private       IResolve _currentPlaying;

		#endregion Fields

		#region Properties

		public UnityVideoPlayer UnityVideoPlayer {
			// ReSharper disable Unity.PerformanceCriticalCodeInvocation
			get => videoPlayer ??= GetComponent<UnityVideoPlayer>() ?? GetComponentInChildren<UnityVideoPlayer>();
			set => videoPlayer = value;
		}

		public AudioSource AudioSource {
			// ReSharper disable Unity.PerformanceCriticalCodeInvocation
			get => audioSource ??= GetComponent<AudioSource>() ?? GetComponentInChildren<AudioSource>();
			set => audioSource = value;
		}

		#endregion Properties

		#region Unity Lifecycle

		private void Awake() {
			UnityVideoPlayer.prepareCompleted += OnPrepareCompleted;
			UnityVideoPlayer.started += OnStarted;
			UnityVideoPlayer.loopPointReached += OnLoopPointReached;
			UnityVideoPlayer.errorReceived += OnErrorReceived;
			UnityVideoPlayer.clockResyncOccurred += OnClockResyncOccurred;
			UnityVideoPlayer.frameDropped += OnFrameDropped;
			UnityVideoPlayer.frameReady += OnFrameReady;
			UnityVideoPlayer.seekCompleted += OnSeekCompleted;
		}

		private void Start() {
			if (!string.IsNullOrEmpty(playQuery))
				Play(playQuery);
		}

		private void Update() {
			// Vérification du statut de lecture
			var isPlaying = UnityVideoPlayer.isPlaying;
			if (isPlaying != _lastPlayingStatus) {
				_lastPlayingStatus = isPlaying;
				onPlayStatusChanged.Invoke(this, isPlaying);
			}

			// Surveiller le progrès de la vidéo seulement si elle est en cours de lecture
			if (isPlaying && UnityVideoPlayer.length > 0) {
				var currentProgress = GetProgress();
				// Déclencher l'événement seulement si le progrès a changé de manière significative (plus de 1%)
				if (!(Math.Abs(currentProgress - _lastProgress) > float.Epsilon)) return;
				_lastProgress = currentProgress;
				onProgress.Invoke(this, currentProgress);
			}
		}

		private void OnDestroy() {
			UnityVideoPlayer.prepareCompleted -= OnPrepareCompleted;
			UnityVideoPlayer.started -= OnStarted;
			UnityVideoPlayer.loopPointReached -= OnLoopPointReached;
			UnityVideoPlayer.errorReceived -= OnErrorReceived;
			UnityVideoPlayer.clockResyncOccurred -= OnClockResyncOccurred;
			UnityVideoPlayer.frameDropped -= OnFrameDropped;
			UnityVideoPlayer.frameReady -= OnFrameReady;
			UnityVideoPlayer.seekCompleted -= OnSeekCompleted;
		}

		#endregion Unity Lifecycle

		#region Events

		public UnityEvent<IVideoPlayer>             onReady = new(); // Événement pour la préparation terminée
		public UnityEvent<IVideoPlayer>             onStart = new(); // Événement pour le début de la lecture
		public UnityEvent<IVideoPlayer>             onEnd = new(); // Événement pour la fin de la lecture
		public UnityEvent<IVideoPlayer, Exception>  onError = new(); // Événement pour les erreurs
		public UnityEvent<IVideoPlayer>             onConnectionLost = new(); // Événement pour perte de connexion
		public UnityEvent<IVideoPlayer>             onReconnecting = new(); // Événement pour tentative de reconnexion
		public UnityEvent<IVideoPlayer>             onReconnected = new(); // Événement pour reconnexion réussie
		public UnityEvent<IVideoPlayer>             onPlay = new(); // Événement pour la lecture
		public UnityEvent<IVideoPlayer>             onPause = new(); // Événement pour la pause
		public UnityEvent<IVideoPlayer>             onResume = new(); // Événement pour la reprise
		public UnityEvent<IVideoPlayer, double>     onProgress = new(); // Événement pour la progression (0.0 à 1.0)
		public UnityEvent<IVideoPlayer, double>     onSeek = new(); // Événement pour le seek (temps en secondes)
		public UnityEvent<IVideoPlayer, float>      onVolumeChanged = new(); // Événement pour le changement de volume (0.0 à 1.0)
		public UnityEvent<IVideoPlayer, bool>       onPlayStatusChanged = new(); // Événement pour le changement de statut de lecture
		public UnityEvent<IVideoPlayer, Vector2Int> onResolutionChanged = new(); // Événement pour le changement de résolution

		public UnityEvent<IVideoPlayer, IFetchOptions>            onResolving = new(); // Événement pour le début de la résolution
		public UnityEvent<IVideoPlayer, IFetchOptions, IResult[]> onResolved = new(); // Événement pour la résolution terminée

		public UnityEvent<IVideoPlayer> OnReadyEvent()
			=> onReady;

		public UnityEvent<IVideoPlayer> OnConnectionLost()
			=> onConnectionLost;

		public UnityEvent<IVideoPlayer> OnReconnecting()
			=> onReconnecting;

		public UnityEvent<IVideoPlayer> OnReconnected()
			=> onReconnected;

		public UnityEvent<IVideoPlayer> OnPlayEvent()
			=> onPlay;

		public UnityEvent<IVideoPlayer> OnPauseEvent()
			=> onPause;

		public UnityEvent<IVideoPlayer> OnResumeEvent()
			=> onResume;

		public UnityEvent<IVideoPlayer, double> OnProgressEvent()
			=> onProgress;

		public UnityEvent<IVideoPlayer, double> OnSeekEvent()
			=> onSeek;

		public UnityEvent<IVideoPlayer, float> OnVolumeChangedEvent()
			=> onVolumeChanged;

		public UnityEvent<IVideoPlayer, bool> OnPlayStatusChangedEvent()
			=> onPlayStatusChanged;

		public UnityEvent<IVideoPlayer, Exception> OnErrorEvent()
			=> onError;

		public UnityEvent<IVideoPlayer> OnStartEvent()
			=> onStart;

		public UnityEvent<IVideoPlayer> OnEndEvent()
			=> onEnd;

		public UnityEvent<IVideoPlayer, IFetchOptions> OnResolvingEvent()
			=> onResolving;

		public UnityEvent<IVideoPlayer, IFetchOptions, IResult[]> OnResolvedEvent()
			=> onResolved;


		public UnityEvent<IVideoPlayer, Vector2Int> OnResolutionChangedEvent()
			=> onResolutionChanged;

		#endregion Events

		#region Resolving

		private IFetchOptions _currentFetchOptions;

		private void Resolve(IFetchOptions fetchOptions) {
			if (_currentFetchOptions != null)
				_currentFetchOptions.GetCancellation().Cancel();
			_currentFetchOptions = fetchOptions;
			Logger.LogDebug($"Resolving {_currentFetchOptions?.ToString() ?? "null"}");
			onResolving.Invoke(this, fetchOptions);
		}

		public void OnResolve(IFetchOptions initial, IResult[] results) {
			if (_currentFetchOptions != initial) return; // Ignore if not the current fetch options

			foreach (var result in results) {
				if (result == null || !result.IsError()) continue;
				Logger.LogWarning($"Error during fetch for {_currentFetchOptions?.ToString() ?? "null"}: {result.GetError()}");
			}

			var resolves = results.SelectMany(e => e.GetData()).ToArray();

			if (resolves.Length == 0) {
				Logger.LogWarning($"No data found for {_currentFetchOptions?.ToString() ?? "null"}");
				onError.Invoke(this, new Exception("No data found"));
				return;
			}

			var first = resolves.FirstOrDefault();

			if (first == null) {
				Logger.LogWarning($"No results found for {_currentFetchOptions?.ToString() ?? "null"}");
				onError.Invoke(this, new Exception("No results found"));
				return;
			}

			var tuple = first.FindQuality();

			if (tuple.Item1 == null) {
				Logger.LogWarning($"No compatible stream found for {_currentFetchOptions?.ToString() ?? "null"}");
				onError.Invoke(this, new Exception("No compatible stream found"));
				return;
			}

			onResolved.Invoke(this, initial, results);
			_currentPlaying = first;

			Play(tuple, IsLooping());
		}

		#endregion Resolving

		#region Playback

		public void Play(string query)
			=> Play(query, IsLooping());


		public void Play(string query, bool loop) {
			Logger.LogDebug($"Play called with query: {query}, loop: {loop}");
			Resolve(new VideoFetchOptions { Query = query });
			SetLooping(loop);
		}

		private void PlayUrl(string url, bool loop) {
			_currentUrl = url; // Stocker l'URL pour les reconnexions
			_reconnectAttempts = 0;   // Réinitialiser le compteur de tentatives
			_isReconnecting = false;

			UnityVideoPlayer.url = url;
			UnityVideoPlayer.isLooping = loop;
			UnityVideoPlayer.Prepare();

			Logger.LogDebug($"Playing URL {_currentUrl}");
			onPlay.Invoke(this);
		}

		private void Play((IFormat, IFormat) tuple, bool loop) {
			if (tuple.Item1 is IAudioVideo av) {
				Play(av);
				return;
			}

			if (tuple is { Item1: IVideo v, Item2: IAudio a }) {
				Play(v, a);
				return;
			}

			switch (tuple.Item1) {
				case IVideo v2:
					Play(v2);
					break;
				case IAudio a2:
					Play(a2);
					break;
				default:
					Logger.LogError("Tuple contains unsupported format types");
					break;
			}
		}

		private void Play(IAudioVideo audioVideo) {
			if (audioVideo == null) {
				Logger.LogError("AudioVideo format is null");
				onError.Invoke(this, new ArgumentNullException(nameof(audioVideo)));
				return;
			}

			var url = audioVideo.GetUrl();
			if (string.IsNullOrEmpty(url)) {
				Logger.LogError("AudioVideo URL is null or empty");
				onError.Invoke(this, new Exception("AudioVideo URL is null or empty"));
				return;
			}

			Logger.LogDebug($"Playing AudioVideo format: {url} (Resolution: {audioVideo.GetResolution()}, Audio Channels: {audioVideo.GetAudioChannels()})");

			// Configure le player pour audio et vidéo
			UnityVideoPlayer.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.AudioSource;
			UnityVideoPlayer.SetTargetAudioSource(0, AudioSource);

			PlayUrl(url, IsLooping());
		}

		private void Play(IVideo video, IAudio audio) {
			if (video == null || audio == null) {
				Logger.LogError($"Video or Audio format is null - Video: {video != null}, Audio: {audio != null}");
				onError.Invoke(this, new ArgumentNullException(video == null ? nameof(video) : nameof(audio)));
				return;
			}

			var videoUrl = video.GetUrl();
			var audioUrl = audio.GetUrl();

			if (string.IsNullOrEmpty(videoUrl) || string.IsNullOrEmpty(audioUrl)) {
				Logger.LogError($"Video or Audio URL is null or empty - Video URL: {!string.IsNullOrEmpty(videoUrl)}, Audio URL: {!string.IsNullOrEmpty(audioUrl)}");
				onError.Invoke(this, new Exception("Video or Audio URL is null or empty"));
				return;
			}

			Logger.LogDebug($"Playing separate Video and Audio streams - Video: {videoUrl}, Audio: {audioUrl}");
			Logger.LogDebug($"Video Resolution: {video.GetResolution()}, Audio Channels: {audio.GetAudioChannels()}");

			// Pour des streams séparés, Unity VideoPlayer ne supporte pas nativement deux URLs
			// On doit utiliser la vidéo comme source principale et gérer l'audio séparément
			Logger.LogWarning("Separate video/audio streams detected. Using video stream only - audio stream will be ignored.");

			Play(video);
		}

		private void Play(IVideo video) {
			if (video == null) {
				Logger.LogError("Video format is null");
				onError.Invoke(this, new ArgumentNullException(nameof(video)));
				return;
			}

			var url = video.GetUrl();
			if (string.IsNullOrEmpty(url)) {
				Logger.LogError("Video URL is null or empty");
				onError.Invoke(this, new Exception("Video URL is null or empty"));
				return;
			}

			Logger.LogDebug($"Playing Video format: {url} (Resolution: {video.GetResolution()}, Framerate: {video.GetFramerate()})");

			// Configure le player pour vidéo seulement
			UnityVideoPlayer.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.None;

			PlayUrl(url, IsLooping());
		}

		private void Play(IAudio audio) {
			if (audio == null) {
				Logger.LogError("Audio format is null");
				onError.Invoke(this, new ArgumentNullException(nameof(audio)));
				return;
			}

			var url = audio.GetUrl();
			if (string.IsNullOrEmpty(url)) {
				Logger.LogError("Audio URL is null or empty");
				onError.Invoke(this, new Exception("Audio URL is null or empty"));
				return;
			}

			Logger.LogDebug($"Playing Audio format: {url} (Channels: {audio.GetAudioChannels()}, Bitrate: {audio.GetAudioBitrate()})");

			// Configure le player pour audio seulement
			UnityVideoPlayer.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.AudioSource;
			UnityVideoPlayer.SetTargetAudioSource(0, AudioSource);

			PlayUrl(url, IsLooping());
		}

		public void Stop() {
			if (!UnityVideoPlayer.isPlaying) return;
			UnityVideoPlayer.Stop();
		}

		public void Pause() {
			if (!UnityVideoPlayer.isPlaying) return;
			UnityVideoPlayer.Pause();
			onPause.Invoke(this);
		}

		public void Resume() {
			if (UnityVideoPlayer.isPlaying) return;
			UnityVideoPlayer.Play();
			onResume.Invoke(this);
		}

		public void SetVolume(float volume) {
			var clampedVolume = Mathf.Clamp01(volume);
			AudioSource.volume = clampedVolume;
			onVolumeChanged.Invoke(this, clampedVolume);
		}

		public void SetSeek(double time) {
			var clampedTime = Math.Clamp(time, 0, UnityVideoPlayer.length);
			UnityVideoPlayer.time = clampedTime;
			onSeek.Invoke(this, clampedTime);
		}

		public double GetTime()
			=> UnityVideoPlayer.time;

		public double GetProgress()
			=> UnityVideoPlayer.length > 0 ? UnityVideoPlayer.time / UnityVideoPlayer.length : 0;

		public double GetDuration()
			=> UnityVideoPlayer.length;

		public bool IsLooping()
			=> UnityVideoPlayer.isLooping;

		public void SetLooping(bool loop)
			=> UnityVideoPlayer.isLooping = loop;

		public RenderTexture GetRender()
			=> UnityVideoPlayer.targetTexture;


		public float GetVolume()
			=> AudioSource.volume;

		public bool IsPlaying()
			=> UnityVideoPlayer.isPlaying;

		#endregion Playback

		#region Callbacks

		private void OnPrepareCompleted(UnityVideoPlayer source) {
			Logger.Log($"Video prepared: {source.url}");

			// Si c'était une reconnexion réussie
			if (_isReconnecting) {
				_isReconnecting = false;
				_reconnectAttempts = 0;
				Logger.Log("Reconnexion réussie !");
				onReconnected.Invoke(this);
			}

			onReady.Invoke(this);
		}

		private void OnStarted(UnityVideoPlayer source) {
			Logger.Log($"Video started: {source.url}");
			onStart.Invoke(this);
		}

		private void OnLoopPointReached(UnityVideoPlayer source) {
			Logger.Log($"Video ended: {source.url}");
			onEnd.Invoke(this);
		}

		private void OnErrorReceived(UnityVideoPlayer source, string message) {
			var exception = new Exception(message);
			Logger.LogError($"Video error: {source.url} - {message}");

			// Vérifier si c'est un problème de connexion
			if (IsConnectionError(message) && !string.IsNullOrEmpty(_currentUrl)) {
				HandleConnectionError();
			} else {
				// Erreur non liée à la connexion, propager l'erreur
				onError.Invoke(this, exception);
			}
		}

		private void OnSeekCompleted(UnityVideoPlayer source) {
			Logger.Log($"Seek completed on video: {source.url}");
		}

		private void OnClockResyncOccurred(UnityVideoPlayer source, double seconds) {
			Logger.Log($"Clock resync occurred: {seconds}s");
		}

		private void OnFrameDropped(UnityVideoPlayer source) {
			Logger.LogWarning("Frame dropped");
		}

		private void OnFrameReady(UnityVideoPlayer source, long frameIdx) {
			// Frame ready - peut être utilisé pour des traitements spécifiques
		}

		#endregion Callbacks

		#region Connection Handling

		/// <summary>
		/// Détermine si l'erreur est liée à un problème de connexion
		/// </summary>
		private static bool IsConnectionError(string errorMessage) {
			var connectionErrors = new[] {
				"network", "connection", "timeout", "unreachable",
				"dns", "host", "failed to connect", "cannot connect",
				"internet", "offline", "502", "503", "504"
			};

			return connectionErrors
				.Any(error => errorMessage.ToLower().Contains(error));
		}

		/// <summary>
		/// Gère les erreurs de connexion avec tentatives de reconnexion
		/// </summary>
		private void HandleConnectionError() {
			if (!_isReconnecting) {
				Logger.Log("Perte de connexion détectée");
				onConnectionLost.Invoke(this);
			}

			if (_reconnectAttempts < MaxReconnectAttempts) {
				_reconnectAttempts++;
				_isReconnecting = true;

				Logger.Log($"Tentative de reconnexion {_reconnectAttempts}/{MaxReconnectAttempts}");
				onReconnecting.Invoke(this);

				AttemptReconnectionAsync().Forget();
			} else {
				Logger.LogError("Échec de la reconnexion après plusieurs tentatives");
				_isReconnecting = false;
				onError.Invoke(this, new Exception($"Impossible de se reconnecter après {MaxReconnectAttempts} tentatives"));
			}
		}

		/// <summary>
		/// Tente la reconnexion avec délai en utilisant UniTask
		/// </summary>
		private async UniTaskVoid AttemptReconnectionAsync() {
			await UniTask.Delay(TimeSpan.FromSeconds(ReconnectDelay), cancellationToken: this.GetCancellationTokenOnDestroy());

			if (!string.IsNullOrEmpty(_currentUrl)) {
				Logger.Log($"Reconnexion en cours vers: {_currentUrl}");
				UnityVideoPlayer.url = _currentUrl;
				UnityVideoPlayer.Prepare();
			}
		}

		/// <summary>
		/// Force une reconnexion manuelle
		/// </summary>
		public void ForceReconnect() {
			if (string.IsNullOrEmpty(_currentUrl)) return;
			_reconnectAttempts = 0;
			_isReconnecting = true;
			onReconnecting.Invoke(this);
			AttemptReconnectionAsync().Forget();
		}

		#endregion Connection Handling

		#region Metadata

		public string GetTitle()
			=> _currentPlaying?.GetTile();

		public string GetSubtitle()
			=> _currentPlaying?.GetSubtitle();

		public Vector2Int GetResolution()
			=> UnityVideoPlayer.texture
				? new Vector2Int(UnityVideoPlayer.texture.width, UnityVideoPlayer.texture.height)
				: Vector2Int.zero;

		#endregion Metadata
	}
}
#endif*/