// using System;
// using System.Collections;
// using System.Collections.Generic;
// using SDK.video;
// using UnityEngine;
// using UnityEngine.Events;
// using UnityEngine.Video;
// using UVideoPlayer = UnityEngine.Video.VideoPlayer;
// using Logger = Nox.CCK.Utils.Logger;
//
// namespace Nox.CCK.VideoPlayer {
// 	public class VideoPlayer : MonoBehaviour, IVideoPlayer, IVideoPlayerEvents {
// 		[Header("Video Components")]
// 		public UVideoPlayer player;
// 		public AudioSource audioSource; // Composant pour l'audio externe
//
// 		public SurroundAudioManager surroundAudioManager;
//
// 		[Header("Stream Configuration")]
// 		public bool useSeparateAudioUrl; // Option pour activer l'audio séparé
//
// 		public AudioSpeakerMode preferredAudioMode = AudioSpeakerMode.Mode5point1;
//
// 		[Header("Autoplay Configuration")]
// 		public bool autoPlayOnStart; // Active l'autoplay au démarrage
// 		public AutoplayMode autoplayMode = AutoplayMode.VideoOnly; // Mode d'autoplay
// 		
// 		[Space(5)]
// 		[Tooltip("URL de la vidéo à lire automatiquement")]
// 		public string defaultVideoUrl = "";
// 		
// 		[Tooltip("URL de l'audio à lire automatiquement (si mode séparé activé)")]
// 		public string defaultAudioUrl = "";
//
// 		// Propriétés privées pour l'état
// 		private string _currentVideoUrl;
// 		private string _currentAudioUrl;
// 		private bool   _isInitialized;
// 		private bool   _isAudioSyncing;
//
// 		// Cache des informations de flux
// 		private Vector2              _videoResolution;
// 		private float                _frameRate;
// 		private string               _videoCodec;
// 		private string               _audioCodec;
// 		private List<AudioTrackInfo> _audioTracks = new();
//
// 		#region IVideoPlayer Properties
//
// 		public bool IsPlaying
// 			=> player && player.isPlaying;
//
// 		public bool IsPaused
// 			=> player && player.isPaused;
//
// 		public float Duration
// 			=> player ? (float)player.length : 0f;
//
// 		public float CurrentTime {
// 			get => player ? (float)player.time : 0f;
// 			set => Seek(value);
// 		}
//
// 		public float Volume {
// 			get {
// 				if (useSeparateAudioUrl && audioSource)
// 					return audioSource.volume;
// 				if (surroundAudioManager)
// 					return surroundAudioManager.masterVolume;
// 				if (player && player.audioTrackCount > 0)
// 					return player.GetDirectAudioVolume(0);
// 				return 1f; // valeur par défaut
// 			}
// 			set {
// 				// Gérer le volume pour l'audio séparé
// 				if (useSeparateAudioUrl && audioSource) {
// 					audioSource.volume = value;
// 				}
// 				
// 				if (surroundAudioManager) {
// 					surroundAudioManager.SetMasterVolume(value);
// 				} else if (player) {
// 					for (ushort i = 0; i < player.audioTrackCount; i++) {
// 						player.SetDirectAudioVolume(i, value);
// 					}
// 				}
// 			}
// 		}
//
// 		public bool IsMuted {
// 			get {
// 				if (useSeparateAudioUrl && audioSource)
// 					return audioSource.mute;
// 				if (player && player.audioTrackCount > 0)
// 					return player.GetDirectAudioMute(0);
// 				return false; // valeur par défaut
// 			}
// 			set {
// 				// Gérer le mute pour l'audio séparé
// 				if (useSeparateAudioUrl && audioSource) {
// 					audioSource.mute = value;
// 				}
// 				
// 				if (!player) return;
// 				for (ushort i = 0; i < player.audioTrackCount; i++) {
// 					player.SetDirectAudioMute(i, value);
// 				}
// 			}
// 		}
//
// 		public string VideoStreamUrl {
// 			get => _currentVideoUrl;
// 			set => _currentVideoUrl = value;
// 		}
//
// 		public string AudioStreamUrl {
// 			get => _currentAudioUrl;
// 			set => _currentAudioUrl = value;
// 		}
//
// 		public AudioSpeakerMode SpeakerMode {
// 			get => surroundAudioManager?.GetCurrentConfiguration()?.mode ?? AudioSpeakerMode.Stereo;
// 			set => SetAudioChannelConfiguration(value);
// 		}
//
// 		public bool IsSurroundSound
// 			=> surroundAudioManager?.IsSurroundSoundCapable() ?? false;
//
// 		#endregion
//
// 		#region Unity Lifecycle
//
// 		private void Awake() {
// 			InitializeComponents();
// 		}
//
// 		private void Start() {
// 			SetupEventHandlers();
// 			Initialize();
// 		}
//
// 		private void InitializeComponents() {
// 			player               ??= gameObject.GetComponent<UVideoPlayer>();
// 			surroundAudioManager ??= GetComponent<SurroundAudioManager>();
// 		}
//
// 		private void SetupEventHandlers() {
// 			if (player) {
// 				player.prepareCompleted += OnVideoPrepared;
// 				player.started          += OnVideoStarted;
// 				player.loopPointReached += OnVideoEnded;
// 				player.errorReceived    += OnVideoError;
// 			}
//
// 			if (surroundAudioManager)
// 				surroundAudioManager.OnAudioModeChanged += OnAudioModeChanged;
// 		}
//
// 		#endregion
//
// 		#region IVideoPlayer Methods
//
// 		public void Play() {
// 			EnsurePlayerInitialized();
// 			
// 			if (useSeparateAudioUrl && audioSource && audioSource.clip) {
// 				// Synchroniser la lecture vidéo et audio
// 				StartCoroutine(SynchronizePlayback());
// 			} else {
// 				player?.Play();
// 			}
// 		}
//
// 		public void Pause() {
// 			EnsurePlayerInitialized();
// 			player?.Pause();
// 			
// 			if (useSeparateAudioUrl && audioSource) {
// 				audioSource.Pause();
// 			}
// 		}
//
// 		public void Stop() {
// 			EnsurePlayerInitialized();
// 			player?.Stop();
// 			
// 			if (useSeparateAudioUrl && audioSource) {
// 				audioSource.Stop();
// 			}
// 		}
//
// 		public void Seek(float time) {
// 			EnsurePlayerInitialized();
// 			if (!player) return;
// 			
// 			player.time = time;
// 			
// 			if (useSeparateAudioUrl && audioSource && audioSource.clip) {
// 				// Synchroniser l'audio avec la nouvelle position
// 				audioSource.time = time;
// 			}
// 		}
//
// 		/// <summary>
// 		/// S'assure que le composant VideoPlayer est initialisé avant utilisation
// 		/// </summary>
// 		private void EnsurePlayerInitialized() {
// 			if (player == null) {
// 				Logger.LogWarning("VideoPlayer non initialisé, tentative d'initialisation...");
// 				InitializeComponents();
// 			}
// 		}
//
// 		public void LoadUrl(string url) {
// 			if (string.IsNullOrEmpty(url)) return;
//
// 			_currentVideoUrl = url;
//
// 			if (player) {
// 				Logger.LogDebug($"Chargement de la vidéo depuis: {url}");
// 				player.source = VideoSource.Url;
// 				player.url    = url;
// 				
// 				// Si on utilise un audio séparé, désactiver l'audio du VideoPlayer
// 				if (useSeparateAudioUrl && !string.IsNullOrEmpty(_currentAudioUrl)) {
// 					player.audioOutputMode = VideoAudioOutputMode.None;
// 				} else {
// 					player.audioOutputMode = VideoAudioOutputMode.AudioSource;
// 				}
// 				
// 				player.Prepare();
// 			}
// 		}
//
// 		/// <summary>
// 		/// Charge une vid��o et un audio depuis des URLs séparées
// 		/// </summary>
// 		/// <param name="videoUrl">URL de la vidéo</param>
// 		/// <param name="audioUrl">URL de l'audio</param>
// 		public void LoadSeparateUrls(string videoUrl, string audioUrl) {
// 			if (string.IsNullOrEmpty(videoUrl)) return;
//
// 			useSeparateAudioUrl = !string.IsNullOrEmpty(audioUrl);
// 			_currentVideoUrl = videoUrl;
// 			_currentAudioUrl = audioUrl;
//
// 			Logger.LogDebug($"Chargement vidéo: {videoUrl}");
// 			if (useSeparateAudioUrl) {
// 				Logger.LogDebug($"Chargement audio: {audioUrl}");
// 			}
//
// 			// Charger la vidéo
// 			if (player) {
// 				player.source = VideoSource.Url;
// 				player.url = videoUrl;
// 				player.audioOutputMode = useSeparateAudioUrl ? VideoAudioOutputMode.None : VideoAudioOutputMode.AudioSource;
// 				player.Prepare();
// 			}
//
// 			// Charger l'audio séparé si nécessaire
// 			if (useSeparateAudioUrl) {
// 				LoadSeparateAudio(audioUrl);
// 			}
// 		}
//
// 		/// <summary>
// 		/// Charge un flux audio séparé
// 		/// </summary>
// 		/// <param name="audioUrl">URL du flux audio</param>
// 		public void LoadSeparateAudio(string audioUrl) {
// 			if (string.IsNullOrEmpty(audioUrl)) return;
//
// 			_currentAudioUrl = audioUrl;
//
// 			// S'assurer qu'on a un AudioSource
// 			if (audioSource == null) {
// 				audioSource = gameObject.GetComponent<AudioSource>();
// 				if (audioSource == null) {
// 					audioSource = gameObject.AddComponent<AudioSource>();
// 				}
// 			}
//
// 			// Charger l'audio via une coroutine
// 			StartCoroutine(LoadAudioFromUrl(audioUrl));
// 		}
//
// 		/// <summary>
// 		/// Coroutine pour charger l'audio depuis une URL
// 		/// </summary>
// 		private IEnumerator LoadAudioFromUrl(string audioUrl) {
// 			Logger.LogDebug($"Chargement de l'audio depuis: {audioUrl}");
// 			
// 			using (var www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(audioUrl, AudioType.MPEG)) {
// 				yield return www.SendWebRequest();
//
// 				if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success) {
// 					Logger.LogError($"Erreur lors du chargement de l'audio: {www.error}");
// 					onError.Invoke(new Exception($"Erreur audio: {www.error}"));
// 				} else {
// 					var audioClip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
// 					if (audioSource) {
// 						audioSource.clip = audioClip;
// 						audioSource.loop = player ? player.isLooping : false;
// 						Logger.LogDebug("Audio chargé avec succès");
// 					}
// 				}
// 			}
// 		}
//
// 		public void LoadVideoClip(VideoClip clip) {
// 			if (!player || !clip) return;
// 			player.source = VideoSource.VideoClip;
// 			player.clip   = clip;
// 			player.Prepare();
// 		}
//
// 		public void SetAudioChannelConfiguration(AudioSpeakerMode mode)
// 			=> surroundAudioManager?.SetAudioConfiguration(mode);
//
// 		public void SetAudioTrack(int trackIndex) {
// 			if (player != null && trackIndex >= 0 && trackIndex < _audioTracks.Count) {
// 				// Utiliser SetDirectAudioVolume pour contrôler les pistes audio
// 				// D'abord, couper toutes les pistes
// 				for (ushort i = 0; i < player.audioTrackCount; i++) {
// 					player.SetDirectAudioVolume(i, 0f);
// 				}
//
// 				// Activer seulement la piste sélectionnée
// 				if (trackIndex < player.audioTrackCount) {
// 					player.SetDirectAudioVolume((ushort)trackIndex, Volume);
// 				}
// 			}
// 		}
//
// 		public int GetAudioTrackCount()
// 			=> _audioTracks.Count;
//
// 		public string GetAudioTrackLanguage(int trackIndex) {
// 			if (trackIndex >= 0 && trackIndex < _audioTracks.Count)
// 				return _audioTracks[trackIndex].language;
// 			return "";
// 		}
//
// 		public Vector2 GetVideoResolution()
// 			=> _videoResolution;
//
//
// 		public float GetFrameRate()
// 			=> _frameRate;
//
//
// 		public string GetVideoCodec()
// 			=> _videoCodec;
//
//
// 		public string GetAudioCodec()
// 			=> _audioCodec;
//
// 		#endregion
//
// 		#region Private Methods
//
// 		private void Initialize() {
// 			if (_isInitialized) return;
// 			Logger.LogDebug("Initialisation du VideoPlayer");
//
// 			// Configuration du VideoPlayer Unity
// 			if (player) {
// 				player.renderMode      = VideoRenderMode.RenderTexture;
// 				player.audioOutputMode = VideoAudioOutputMode.AudioSource;
// 				player.playOnAwake     = false;
// 			}
//
// 			// Configuration audio surround par défaut
// 			surroundAudioManager?.SetAudioConfiguration(preferredAudioMode);
//
// 			_isInitialized = true;
//
// 			// Démarrer l'autoplay si activé
// 			if (autoPlayOnStart) {
// 				StartCoroutine(StartAutoplay());
// 			}
// 		}
//
// 		/// <summary>
// 		/// Démarre l'autoplay selon la configuration
// 		/// </summary>
// 		private IEnumerator StartAutoplay() {
// 			// Attendre une frame pour s'assurer que tout est initialisé
// 			yield return null;
//
// 			Logger.LogDebug($"Démarrage de l'autoplay en mode: {autoplayMode}");
//
// 			switch (autoplayMode) {
// 				case AutoplayMode.VideoOnly:
// 					if (!string.IsNullOrEmpty(defaultVideoUrl)) {
// 						LoadUrl(defaultVideoUrl);
// 						yield return new WaitUntil(() => player && player.isPrepared);
// 						Play();
// 					}
// 					break;
//
// 				case AutoplayMode.AudioOnly:
// 					if (!string.IsNullOrEmpty(defaultAudioUrl)) {
// 						LoadSeparateAudio(defaultAudioUrl);
// 						// L'audio commencera automatiquement après le chargement
// 					}
// 					break;
//
// 				case AutoplayMode.VideoAndAudio:
// 					if (!string.IsNullOrEmpty(defaultVideoUrl)) {
// 						if (!string.IsNullOrEmpty(defaultAudioUrl)) {
// 							// Charger vidéo et audio séparés
// 							LoadSeparateUrls(defaultVideoUrl, defaultAudioUrl);
// 						} else {
// 							// Charger seulement la vidéo (avec son audio intégré)
// 							LoadUrl(defaultVideoUrl);
// 						}
// 						yield return new WaitUntil(() => player && player.isPrepared);
// 						Play();
// 					}
// 					break;
// 			}
// 		}
//
// 		#endregion
//
// 		#region Event Handlers
//
// 		private void OnVideoPrepared(UVideoPlayer source) {
// 			Logger.Log("Vidéo prête à être lue");
// 			onReady.Invoke();
// 		}
//
// 		private void OnVideoStarted(UVideoPlayer source) {
// 			Logger.Log("Lecture vidéo commencée");
// 			onStart.Invoke();
// 		}
//
// 		private void OnVideoEnded(UVideoPlayer source) {
// 			Logger.Log("Lecture vidéo terminée");
// 			onEnd.Invoke();
// 		}
//
// 		private void OnVideoError(UVideoPlayer source, string message) {
// 			Logger.LogError($"Erreur vidéo: {message}");
// 			onError.Invoke(new Exception(message));
// 		}
//
// 		private void OnAudioModeChanged(AudioSpeakerMode newMode) {
// 			Logger.Log($"Mode audio changé vers: {newMode}");
// 		}
//
// 		#endregion
//
// 		/// <summary>
// 		/// Coroutine pour synchroniser la lecture vidéo et audio
// 		/// </summary>
// 		private IEnumerator SynchronizePlayback() {
// 			if (!player || !audioSource) yield break;
// 			
// 			_isAudioSyncing = true;
// 			
// 			// Démarrer la vidéo
// 			player.Play();
// 			
// 			// Attendre que la vidéo commence vraiment
// 			yield return new WaitUntil(() => player.isPlaying);
// 			
// 			// Démarrer l'audio en même temps
// 			if (audioSource.clip) {
// 				audioSource.Play();
// 			}
// 			
// 			_isAudioSyncing = false;
// 		}
// 	}
//
// 	[Serializable]
// 	public class AudioTrackInfo {
// 		public string language;
// 		public int    channels;
// 		public int    sampleRate;
// 		public string codec;
// 	}
//
// 	public enum AutoplayMode {
// 		VideoOnly,
// 		AudioOnly,
// 		VideoAndAudio
// 	}
// }
