using System;
using System.Collections.Generic;
using Nox.CCK.Language;
using Nox.CCK.UI;
using Nox.CCK.Utils;
using Nox.UI.Audio;
using Nox.VideoPlayer.Runtime.Helpers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.VideoPlayer.Runtime.Clients {
	public class VideoPlayerComponent : MonoBehaviour {
		private VideoPlayerPage _page;

		public AspectRatioFitter ratio;
		public RawImage video;
		public Slider seek;
		public Slider loaded;
		public TextLanguage current;
		public TextLanguage total;
		public Button center;
		public Image centerIcon;
		public TextLanguage title;
		public TextLanguage subtitle;

		[Header("Bottom bar")]
		[Tooltip("Bar shown for a media with a duration (Reference 'video_player.media_container').")]
		public GameObject mediaContainer;
		[Tooltip("Bar shown for a live stream: no total, no seek bar (Reference 'video_player.live_container').")]
		public GameObject liveContainer;
		[Tooltip("Elapsed time of the live bar.")]
		public TextLanguage liveCurrent;

		[Header("States")]
		[Tooltip("Loading/buffering indicator authored in the video player prefab (Reference " +
			"'video_player.loading'). Its Nox.CCK.UI SpriteAnimation owns the sheet, the frame count " +
			"and the speed.")]
		public GameObject loading;

		[Header("Hover")]
		[Tooltip("While the video plays, the play/pause button is hidden as soon as no pointer/interactor " +
			"is on the page or the pointer stopped moving on it — unless it rests on the button itself, " +
			"which is never hidden under the pointer. It stays visible for this many seconds after the " +
			"last pointer movement.")]
		public float hoverHideDelay = 2f;

		[Tooltip("Same as 'hoverHideDelay' for the play/pause button alone: it only has to be there while " +
			"the user reaches for it, so it goes away much sooner than the bars and the titles.")]
		public float centerHideDelay = 0.35f;

		/// <summary>Search button shown at the top-right of the title (null when absent).</summary>
		private GameObject _searchButton;
		/// <summary>Last applied visibility of the search button (avoid a LINQ lookup per frame).</summary>
		private bool _searchAllowed;

		/// <summary>Button toggling where the media is heard (null when absent).</summary>
		private GameObject _outputButton;
		/// <summary>Icon of the toggle: <c>cast</c> from the world, <c>headphones</c> from the menu.</summary>
		private Image _outputIcon;
		private bool _outputShown;
		/// <summary>Last mode drawn on the toggle.</summary>
		private bool _outputDraw;

		/// <summary>Whether the media is heard from the menu instead of the place of its player in the world.</summary>
		private bool _menuOutput;
		/// <summary>Player whose media sources are muted while the sound is heard from the menu.</summary>
		private IVideoPlayer _outputPlayer;
		private AudioSource[] _outputWorld = Array.Empty<AudioSource>();
		private bool[] _outputMuted;
		/// <summary>Menu source the media is played on while the sound is heard from the menu.</summary>
		private AudioSource _outputSource;
		private IAudioPlay _outputPlay;
		/// <summary>Whether the page is hidden: the media is heard from the world meanwhile, the choice is kept.</summary>
		private bool _soundSuspended;

		// Seek handling: the timeline must stay stable and immediate while seeking.
		private bool _isUserSeeking;
		private bool _live;

		/// Player whose bottom bar has already been resolved (see <see cref="UpdateStream"/>).
		private IVideoPlayer _streamPlayer;

		/// <summary>CanvasGroup fading the play/pause button in and out (Reference of the prefab).</summary>
		private CanvasGroup _center;
		/// <summary>Groups holding the information drawn over the video (bars, titles, actions), faded when idle.</summary>
		private readonly List<CanvasGroup> _chrome = new();
		/// <summary>Number of pointers (mouse, UI ray...) currently on the page.</summary>
		private int _hoverCount;
		/// <summary>Whether at least one pointer is on the page.</summary>
		private bool _hovered;
		/// <summary>Number of pointers currently on the play/pause button itself.</summary>
		private int _buttonHoverCount;
		/// <summary>Whether a pointer rests on the play/pause button (it must not fade out then).</summary>
		private bool _buttonHovered;
		/// <summary>Unscaled time of the last pointer activity on the page (enter or move).</summary>
		private float _lastActivityAt;
		/// <summary>Fade duration of the play/pause button.</summary>
		private const float CenterFade = 0.2f;

		public static (GameObject, VideoPlayerComponent) Generate(VideoPlayerPage page, RectTransform parent) {
			var content = Instantiate(Client.GetAsset<GameObject>("ui:prefabs/split.prefab"), parent);

			var component = content.AddComponent<VideoPlayerComponent>();
			component._page = page;
			content.name    = $"[{page.GetKey()}_{content.GetId()}]";
			var splitContent = Reference.GetComponent<RectTransform>("content", content);

			// generate dashboard
			var container = Instantiate(Client.GetAsset<GameObject>("ui:prefabs/container_full.prefab"), splitContent);
			var videoPlayer = Instantiate(
				Client.GetAsset<GameObject>("ui:prefabs/video_player.prefab"),
				Reference.GetComponent<RectTransform>("content", container)
			);
			component.video          = Reference.GetComponent<RawImage>("video", videoPlayer);
			component.ratio          = Reference.GetComponent<AspectRatioFitter>("ratio", videoPlayer);
			component.video.material = Instantiate(Client.GetAsset<Material>("ui:materials/video_texture.mat"));

			component.seek                = Reference.GetComponent<Slider>("seek", container);
			component.current             = Reference.GetComponent<TextLanguage>("current", container);
			component.total               = Reference.GetComponent<TextLanguage>("total", container);
			component.loaded              = Reference.GetComponent<Slider>("loaded", container);
			component.center              = Reference.GetComponent<Button>("center", videoPlayer);
			component.centerIcon          = Reference.GetComponent<Image>("image", component.center.gameObject);
			component.loaded.interactable = false;

			component._center = Reference.GetComponent<CanvasGroup>("video_player.center_group", videoPlayer);
			component.AddHoverProbe(Reference.GetComponent<PageHoverProbe>("video_player.hover", videoPlayer));
			component.AddHoverProbe(container.GetOrAddComponent<PageHoverProbe>());
			// the button has its own probe: resting on it keeps it visible (see UpdateCenterVisibility)
			component.AddButtonProbe(component.center.gameObject.GetOrAddComponent<PageHoverProbe>());

			component.seek.onValueChanged.AddListener(component.OnSeekValueChanged);
			var trigger          = component.seek.gameObject.GetOrAddComponent<EventTrigger>();
			var pointerDownEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
			pointerDownEntry.callback.AddListener(_ => component.OnSeekStart());
			trigger.triggers.Add(pointerDownEntry);
			var pointerUpEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
			pointerUpEntry.callback.AddListener(_ => component.OnSeekEnd());
			trigger.triggers.Add(pointerUpEntry);
			var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
			dragEntry.callback.AddListener(_ => component.OnSeekDrag());
			trigger.triggers.Add(dragEntry);
			component.center.onClick.AddListener(page.TogglePlayPause);

			component.title    = Reference.GetComponent<TextLanguage>("title", container);
			component.subtitle = Reference.GetComponent<TextLanguage>("subtitle", container);
			component.CreateSearchButton(videoPlayer);
			component.CreateOutputButton(videoPlayer);
			component.ResolveChrome();

			// bottom bar: the media (seek) and live variants are exclusive
			component.mediaContainer = Reference.GetReference("video_player.media_container", videoPlayer);
			component.liveContainer  = Reference.GetReference("video_player.live_container", videoPlayer);
			component.liveCurrent    = FindLiveTime(component.liveContainer);
			component.ResolveLoading(videoPlayer);

			return (content, component);
		}

		/// <summary>
		/// Appends the search action button into the header actions container
		/// (<c>actions_contanier</c> reference of the video_player prefab). The button opens
		/// <see cref="VideoSearchPage"/>; it is only displayed when the current player is
		/// not read-only (see <see cref="IVideoPlayerControl"/>).
		/// </summary>
		private void CreateSearchButton(GameObject videoPlayer) {
			// The container of the header actions is declared on the video_player prefab.
			var actions = Actions(videoPlayer);
			if (!actions) {
				Logger.LogWarning(
					"No 'actions_contanier' reference found in the video player prefab",
					this, nameof(VideoPlayerComponent)
				);
				return;
			}

			var go = Instantiate(Client.GetAsset<GameObject>("ui:prefabs/header_button.prefab"), actions.transform);
			var icon = Reference.GetComponent<Image>("image", go);
			if (icon) icon.sprite = Client.GetAsset<Sprite>("ui:icons/search.png");
			Reference.GetComponent<TextLanguage>("text", go)?.UpdateText("video_player.search.button");

			var button = Reference.GetComponent<Button>("button", go);
			if (button) button.onClick.AddListener(OnSearchClicked);

			_searchButton = go;
			_searchButton.SetActive(false);
		}

		private void OnSearchClicked()
			=> _page?.OpenSearch();

		/// <summary>
		/// Appends the toggle that picks where the media is heard: out of its screen in the world (<c>cast</c>), or
		/// from the menu (<c>headphones</c>) — the sources of the world are then muted and the same media is played
		/// on a 2D source of the menu, so neither the distance nor the reverb zones of the map colour it.
		/// </summary>
		private void CreateOutputButton(GameObject videoPlayer) {
			var actions = Actions(videoPlayer);
			if (!actions)
				return;

			var go = Instantiate(Client.GetAsset<GameObject>("ui:prefabs/header_button.prefab"), actions.transform);
			_outputIcon = Reference.GetComponent<Image>("image", go);

			var label = Reference.GetComponent<TextLanguage>("text", go);
			if (label) label.gameObject.SetActive(false);

			var button = Reference.GetComponent<Button>("button", go);
			if (button) button.onClick.AddListener(OnOutputClicked);

			_outputButton = go;
			_outputButton.SetActive(false);
			DrawOutput(true);
		}

		/// <summary>Container of the header action buttons (Reference <c>actions_contanier</c> of the prefab).</summary>
		private static GameObject Actions(GameObject videoPlayer)
			=> Reference.GetReference("actions_contanier", videoPlayer)
			   ?? Reference.GetReference("actions_contanier");

		private void OnOutputClicked() {
			_menuOutput = !_menuOutput;

			var player = _page?.GetSelectedPlayer();
			_page?.SetMenuOutput(player, _menuOutput);
			ApplyOutput(player);
			UpdateOutputButton(player);
		}

		/// <summary>Gives the sound of the world back to a player that goes away, and resets its toggle.</summary>
		internal void ReleaseOutput(IVideoPlayer player) {
			if (player == null || player != _outputPlayer)
				return;

			RestoreOutput();

			_menuOutput = false;
			UpdateOutputButton(null);
			DrawOutput(true);
		}

		/// <summary>
		/// The page is hidden: the media goes back to the world right away, the menu no longer being in front of the
		/// user to hold it. The choice stays, it is put back by <see cref="ShowSound"/> when the page returns.
		/// </summary>
		internal void HideSound() {
			_soundSuspended = true;
			RestoreOutput();
		}

		/// <summary>The page is shown again: puts the media back where the user asked for it.</summary>
		internal void ShowSound(IVideoPlayer player) {
			_soundSuspended = false;
			ApplyOutput(player);
		}

		/// <summary>Draws the icon of the current output: the screen of the world, or the headphones of the menu.</summary>
		private void DrawOutput(bool force = false) {
			if (!_outputIcon || (!force && _outputDraw == _menuOutput))
				return;

			_outputDraw = _menuOutput;
			var icon = Client.GetAsset<Sprite>(_menuOutput ? "ui:icons/headphones.png" : "ui:icons/cast.png");
			if (icon)
				_outputIcon.sprite = icon;
		}

		/// <summary>
		/// Applies the chosen output: in menu mode the media sources of the player are muted and the same clip
		/// is played on the 2D source of the menu, otherwise the sources of the world are restored.
		/// </summary>
		private void ApplyOutput(IVideoPlayer player) {
			RestoreOutput();
			_outputPlayer = player;

			if (!_menuOutput || _soundSuspended)
				return;

			_outputWorld = MediaSources(player);
			if (_outputWorld.Length == 0)
				return;

			_outputMuted = new bool[_outputWorld.Length];
			for (var i = 0; i < _outputWorld.Length; i++) {
				_outputMuted[i]      = _outputWorld[i].mute;
				_outputWorld[i].mute = true;
			}

			PlayOnMenu();
		}

		/// <summary>Unmutes the sources of the world and stops the menu playback (kept until it is needed again).</summary>
		private void RestoreOutput() {
			for (var i = 0; i < _outputWorld.Length; i++) {
				var source = _outputWorld[i];
				if (source)
					source.mute = _outputMuted != null && i < _outputMuted.Length && _outputMuted[i];
			}

			_outputWorld = Array.Empty<AudioSource>();
			_outputMuted = null;
			// the player has to be applied again (see UpdateUI), the world sources being released
			_outputPlayer = null;

			_outputPlay?.Dispose();
			_outputPlay = null;

			if (_outputSource) {
				_outputSource.Stop();
				_outputSource.clip = null;
			}
		}

		/// <summary>
		/// AudioSources the player drives (the clip is the rolling buffer written by the decoder), so they are
		/// exactly the ones to mute — and the clock the menu playback follows.
		/// </summary>
		private AudioSource[] MediaSources(IVideoPlayer player) {
			var go   = player?.GetGameObject();
			var clip = MediaClip(player);
			if (!go || !clip)
				return Array.Empty<AudioSource>();

			var found = new List<AudioSource>();
			foreach (var source in go.GetComponentsInChildren<AudioSource>(true))
				if (source && source.clip == clip && source != _outputSource)
					found.Add(source);

			return found.ToArray();
		}

		private static AudioClip MediaClip(IVideoPlayer player)
			=> player is IVideoPlayerAudio audio ? audio.Clip : null;

		/// <summary>
		/// Plays the media on the menu source, through the audio of the menu so it follows the volume and the mute
		/// of the menu instead of the channel of the world.
		/// </summary>
		private void PlayOnMenu() {
			var clip = MediaClip(_outputPlayer);
			if (!clip)
				return;

			if (!_outputSource) {
				// root level: the sound must keep playing while the page is hidden behind another one
				var go = new GameObject("Video Player Menu Audio");

				_outputSource              = go.AddComponent<AudioSource>();
				_outputSource.playOnAwake  = false;
				_outputSource.loop         = true;
				_outputSource.spatialBlend = 0f;
			}

			_outputPlay?.Dispose();
			_outputPlay = _page?.GetMenu() is IAudioMenu menu ? menu.Play(clip, _outputSource) : null;

			if (_outputPlay is null) {
				_outputSource.clip = clip;
				_outputSource.Play();
			}

			FollowMenu();
		}

		/// <summary>
		/// Keeps the menu playback on the samples of the media: the sources of the world keep playing (muted) and
		/// are the clock, so the menu never drifts — pausing, buffering and seeking are followed as well.
		/// </summary>
		private void FollowMenu() {
			if (!_outputSource || _outputWorld.Length == 0)
				return;

			var clip = _outputSource.clip ? _outputSource.clip : MediaClip(_outputPlayer);
			if (!clip)
				return;

			var clock = Clock();
			if (!clock) {
				if (_outputSource.isPlaying)
					_outputSource.Pause();
				return;
			}

			if (!_outputSource.isPlaying) {
				_outputSource.timeSamples = clock.timeSamples;
				_outputSource.Play();
				return;
			}

			var samples = clip.samples;
			if (samples <= 0)
				return;

			var drift = Mathf.Abs(_outputSource.timeSamples - clock.timeSamples);
			if (drift > samples / 4)
				_outputSource.timeSamples = clock.timeSamples;
		}

		/// <summary>The furthest along of the media sources, which is the read cursor of the decoder.</summary>
		private AudioSource Clock() {
			AudioSource clock   = null;
			var         samples = -1;

			foreach (var source in _outputWorld) {
				if (!source || !source.isPlaying || source.timeSamples <= samples)
					continue;

				samples = source.timeSamples;
				clock   = source;
			}

			return clock;
		}

		/// <summary>Shows the toggle when the player has a sound, and keeps its icon on the current output.</summary>
		private void UpdateOutputButton(IVideoPlayer player) {
			var shown = MediaClip(player) != null;

			if (_outputButton && shown != _outputShown) {
				_outputShown = shown;
				_outputButton.SetActive(shown);
			}

			DrawOutput();
		}

		public void UpdateProgress(IVideoPlayer player, double progress) {
			if (player == null)
				return;

			// no duration (live): no seek bar, no total, only the elapsed time
			if (_live) {
				FormatTime(liveCurrent, player.Time);
				return;
			}

			if (!_isUserSeeking && seek)
				seek.SetValueWithoutNotify((float)progress);
			if (loaded)
				loaded.value = 0;
			FormatTime(current, player.Time);
			FormatTime(total, player.Duration);
		}

		public void UpdatePlayStatus(IVideoPlayer player, State state) {
			if (player == null)
				return;
			// Transient states must not flip the icon: while seeking/buffering the
			// play/pause intent of the underlying playback is unchanged.
			if (state is State.Seeking or State.Buffering or State.Stalled)
				return;
			var iconName = state == State.Playing
				? "ui:icons/pause.png"
				: "ui:icons/play_arrow.png";
			var icon     = Client.GetAsset<Sprite>(iconName);
			if (icon)
				centerIcon.sprite = icon;
		}

		private static void FormatTime(TextLanguage text, double time) {
			if (!text)
				return;
			if (double.IsNaN(time) || double.IsInfinity(time) || time < 0)
				time = 0;
			var ts = System.TimeSpan.FromSeconds(time);
			text.UpdateText(
				ts.Hours > 0 && LanguageManager.Has("video_player.progress.hours")
					? "video_player.progress.hours"
					: "video_player.progress",
				new[] {
					$"{ts.Hours:D2}",
					$"{ts.Minutes:D2}",
					$"{ts.Seconds:D2}",
					$"{ts.Milliseconds:D2}"
				}
			);
		}

		private void Update()
			=> _page.OnUpdate();

		// ── Bottom bar (media / live) ─────────────────────────────────────

		/// <summary>
		/// Called when the player opens another stream (Play, track change...): the
		/// duration is only known at that moment, so this is where the bottom bar is
		/// resolved — no need to check it every frame.
		/// </summary>
		public void UpdateStream(IVideoPlayer player) {
			_streamPlayer = player;
			SetLive(player != null && IsStream(player));

			// the sources and the clip of the media change with the stream: the output has to be applied again
			if (_menuOutput)
				ApplyOutput(player);
		}

		/// <summary>The media sources of the world are muted while the sound is heard from the menu: release them.</summary>
		private void OnDisable()
			=> RestoreOutput();

		/// <summary>The page is shown again: puts the sound back where the user asked for it.</summary>
		private void OnEnable() {
			if (_menuOutput)
				ApplyOutput(_page?.GetSelectedPlayer());
		}

		/// <summary>The media sources of the world are muted while the sound is heard from the menu: release them.</summary>
		private void OnDestroy() {
			RestoreOutput();

			if (_outputSource) {
				_outputSource.gameObject.Destroy();
				_outputSource = null;
			}
		}
		/// <summary>
		/// Whether the player plays a live stream, i.e. a stream without a duration
		/// (see <see cref="IVideoPlayerDetails.IsStream"/>).
		/// </summary>
		private static bool IsStream(IVideoPlayer player)
			=> player is IVideoPlayerDetails details && details.IsStream;

		private void SetLive(bool live) {
			_live = live;
			if (mediaContainer && mediaContainer.activeSelf == live)
				mediaContainer.SetActive(!live);
			if (liveContainer && liveContainer.activeSelf != live)
				liveContainer.SetActive(live);
		}

		/// <summary>
		/// Elapsed time of the live bar: a <c>video_player.live_current</c> Reference
		/// inside the container, or its first child named <c>current</c>.
		/// </summary>
		private static TextLanguage FindLiveTime(GameObject live) {
			if (!live)
				return null;
			var keyed = Reference.GetComponent<TextLanguage>("video_player.live_current", live);
			if (keyed)
				return keyed;
			var child = live.transform.Find("current");
			return child ? child.GetComponent<TextLanguage>() : null;
		}

		public void UpdateRender(IVideoPlayer player, Texture2D texture = null) {
			var render = texture ? texture : player is IVideoPlayerVideo tex ? tex.Texture : null;
			if (!video)
				return;

			// no frame yet: the empty quad (white) must not be drawn at all
			var shown = render != null;
			if (video.enabled != shown)
				video.enabled = shown;

			if (!render || !ratio)
				return;

			video.texture     = render;
			ratio.aspectRatio = (float)render.width / render.height;
		}

		/// <summary>
		/// Called when the decoder hands a new clip (one per media): in menu mode the menu source has to play it, so
		/// the output is applied again, exactly like a new texture is written on the video.
		/// </summary>
		internal void UpdateAudio(IVideoPlayer player) {
			if (player == null || player != _outputPlayer || !_menuOutput)
				return;

			ApplyOutput(player);
		}

		public void UpdateUI() {
			if (!gameObject.activeInHierarchy)
			return;

			// the search button is only offered when the player is not read-only
			var allowed = _page.CanControl;
			if (_searchButton && allowed != _searchAllowed) {
				_searchAllowed = allowed;
				_searchButton.SetActive(allowed);
			}

			var player = _page.GetSelectedPlayer();

			if (player != _outputPlayer) {
				_menuOutput = _page.GetMenuOutput(player);
				ApplyOutput(player);
			} else if (_menuOutput)
				FollowMenu();

			UpdateOutputButton(player);

			if (player == null) {
				UpdateRender(null);
				return;
			}
			if (player != _streamPlayer)
				UpdateStream(player); // another player is displayed: its bar may differ
			UpdateRender(player);
			UpdatePlayStatus(player, player.State);
			UpdateProgress(player, player.Progress);
			UpdateTitle(player);
			UpdateLoading(player.State);
			UpdateCenterVisibility(IsPlaying(player.State));
			UpdateChromeVisibility(IsPlaying(player.State));
		}
		private void UpdateTitle(IVideoPlayer player) {
			if (player == null)
				return;
			var details = player is IVideoPlayerDetails det ? det : null;

			var t = details?.Title;
			if (string.IsNullOrEmpty(t))
				title.UpdateText("video_player.no_title");
			else
				title.UpdateText("video_player.title", new[] { t });

			var s = details?.Subtitle;
			if (string.IsNullOrEmpty(s))
				subtitle.UpdateText("video_player.no_subtitle");
			else
				subtitle.UpdateText("video_player.subtitle", new[] { s });
		}

		private void OnSeekValueChanged(float value) {
			if (_isUserSeeking || _live || !seek)
				return;
			SeekTo(value);
		}

		public void OnSeekStart() {
			_isUserSeeking = true;
		}

		public void OnSeekEnd() {
			_isUserSeeking = false;
			if (_live || !seek)
				return;
			SeekTo(seek.value);
		}

		public void OnSeekDrag() {
			if (_live || !seek)
				return;
			// Live scrubbing: the player reports the requested time immediately
			// (see Player.Time), so the timeline never falls back to 00:00 mid-seek.
			SeekTo(seek.value);
		}

		/// <summary>
		/// Seek the selected player to <paramref name="normalized"/> × its duration (0–1)
		/// and refresh the timeline right away.
		/// </summary>
		private void SeekTo(float normalized) {
			var player = _page.GetSelectedPlayer();
			if (player == null)
				return;
			var duration = player.Duration;
			if (double.IsNaN(duration) || duration <= 0)
				return;
			player.Time = duration * Mathf.Clamp01(normalized);
			UpdateProgress(player, player.Progress);
		}

		// ── Loading indicator ─────────────────────────────────────────────

		/// <summary>
		/// Resolves the loading/buffering indicator authored in the video player prefab
		/// (Reference <c>video_player.loading</c>). The node and its Nox.CCK.UI
		/// <see cref="SpriteAnimation"/> are configured in the prefab; this only owns its
		/// visibility (see <see cref="UpdateLoading"/>) — activating the node starts the animation.
		/// </summary>
		private void ResolveLoading(GameObject videoPlayer) {
			loading = Reference.GetReference("video_player.loading", videoPlayer);
			if (!loading) {
				Logger.LogWarning(
					"No 'video_player.loading' reference found in the video player prefab",
					this, nameof(VideoPlayerComponent)
				);
				return;
			}

			var anime = loading.GetOrAddComponent<SpriteAnimation>();
			// The sheet describes its own layout (a grid of 512x512 tiles, 48 frames).
			anime.Fps    = 30f; // the animation cycle is 1.6s long
			anime.Sprite = Client.GetAsset<Sprite>("ui:icons/loading.png");

			loading.SetActive(false);
		}

		/// <summary>
		/// Shows the indicator while the player repositions (<see cref="State.Seeking"/>), starves
		/// for data (<see cref="State.Buffering"/>/<see cref="State.Stalled"/>) or while the user
		/// scrubs the timeline. Activating the node starts its <see cref="SpriteAnimation"/>.
		/// </summary>
		private void UpdateLoading(State state) {
			if (!loading)
				return;

			bool busy = _isUserSeeking || state is State.Seeking or State.Buffering or State.Stalled;
			if (loading.activeSelf != busy)
				loading.SetActive(busy);
		}

		// ── Hover (hide the play/pause button) ────────────────────────────

		/// <summary>
		/// Subscribes to a pointer enter/exit/move relay (see <see cref="PageHoverProbe"/>). Pointer
		/// events bubble up from any child, so hovering the video image or a control of the relayed
		/// node counts as being on the page; the counters are aggregated so moving between nodes
		/// never flickers.
		/// </summary>
		private void AddHoverProbe(PageHoverProbe probe) {
			if (!probe)
				return;
			probe.HoverChanged.AddListener(OnHoverChanged);
			probe.Moved.AddListener(OnMoved);
		}

		private void OnHoverChanged(bool hovered) {
			_hoverCount = Mathf.Max(0, _hoverCount + (hovered ? 1 : -1));
			_hovered    = _hoverCount > 0;
			if (_hovered)
				_lastActivityAt = Time.unscaledTime;
		}

		/// <summary>
		/// Subscribes to the pointer relay of the play/pause button itself: unlike the rest of the page,
		/// resting on it must not hide it.
		/// </summary>
		private void AddButtonProbe(PageHoverProbe probe) {
			if (!probe)
				return;
			probe.HoverChanged.AddListener(OnButtonHoverChanged);
		}

		private void OnButtonHoverChanged(bool hovered) {
			_buttonHoverCount = Mathf.Max(0, _buttonHoverCount + (hovered ? 1 : -1));
			_buttonHovered    = _buttonHoverCount > 0;
			if (_buttonHovered)
				_lastActivityAt = Time.unscaledTime;
		}

		private void OnMoved()
			=> _lastActivityAt = Time.unscaledTime;

		/// <summary>
		/// Whether the playback intent of the player is "playing". The transient states
		/// (seeking/buffering/stalled) still count as playing — same rule as
		/// <see cref="VideoPlayerPage.TogglePlayPause"/>.
		/// </summary>
		private static bool IsPlaying(State state)
			=> state is State.Playing or State.Seeking or State.Buffering or State.Stalled;

		/// <summary>
		/// Fades the play/pause button out when no pointer/interactor is on the page (and back in
		/// as soon as one enters), so it never masks the video when unused.
		/// While the video plays it also disappears when the pointer stops moving — but a pointer resting
		/// on the button keeps it visible, and so does a paused/stopped playback (to be able to resume).
		/// A small delay avoids flickering on pointer transitions.
		/// </summary>
		private void UpdateCenterVisibility(bool playing) {
			if (!_center)
				return;

			// the loading indicator owns the centre of the video while it is shown
			bool busy = loading && loading.activeSelf;
			bool visible = !busy
				&& (!playing
					|| _isUserSeeking
					|| _buttonHovered
					|| (_hovered && Time.unscaledTime - _lastActivityAt < centerHideDelay));
			float target = visible ? 1f : 0f;

			if (Mathf.Approximately(_center.alpha, target))
				return;

			_center.alpha = Mathf.MoveTowards(
				_center.alpha,
				target,
				Time.unscaledDeltaTime / CenterFade
			);
			// a faded-out button must not swallow a click
			bool clickable = _center.alpha > 0.5f;
			_center.blocksRaycasts = clickable;
			_center.interactable   = clickable;
		}

		/// <summary>Fade of the information panels, slower than the button so they do not blink.</summary>
		private const float ChromeFade = 0.25f;

		/// <summary>
		/// Marks the information drawn over the video: every child of the player that is not the video itself, the
		/// panel behind it or the loading indicator (top and bottom gradients, bars, header actions...), plus the
		/// elements that may be authored outside the player prefab (titles, timeline, action buttons).
		/// </summary>
		private void ResolveChrome() {
			if (!video)
				return;

			var root = video.transform.parent;
			if (!root)
				return;

			for (var i = 0; i < root.childCount; i++) {
				var child = root.GetChild(i);
				if (child == video.transform)
					continue;
				if (loading && child == loading.transform)
					continue;
				if (child.name.Equals("background", StringComparison.OrdinalIgnoreCase))
					continue;

				AddChrome(child);
			}

			MarkChrome(title);
			MarkChrome(subtitle);
			MarkChrome(seek);
			MarkChrome(center);
			MarkChrome(mediaContainer);
			MarkChrome(liveContainer);
			MarkChrome(_searchButton);
			MarkChrome(_outputButton);
		}

		private void MarkChrome(Component mark)
			=> MarkChrome(mark ? mark.transform : null);

		private void MarkChrome(GameObject mark)
			=> MarkChrome(mark ? mark.transform : null);

		private void MarkChrome(Transform mark) {
			if (!mark)
				return;

			var videoRoot = video ? video.transform.parent : null;
			var node      = Climb(mark, videoRoot) ?? Climb(mark, transform);
			if (!node)
				return;

			AddChrome(node);
		}

		/// <summary>The direct child of <paramref name="root"/> that contains <paramref name="mark"/>.</summary>
		private static Transform Climb(Transform mark, Transform root) {
			if (!root)
				return null;

			var node = mark;
			while (node && node.parent != root)
				node = node.parent;

			return node && node.parent == root ? node : null;
		}

		private void AddChrome(Transform node) {
			if (!node || !video)
				return;

			// never fade the video itself, nor anything that holds it
			if (node == video.transform || video.transform.IsChildOf(node))
				return;

			var group = node.gameObject.GetOrAddComponent<CanvasGroup>();

			// the play/pause button fades on its own (see UpdateCenterVisibility)
			if (group == _center || _chrome.Contains(group))
				return;

			_chrome.Add(group);
		}

		/// <summary>
		/// Fades the information of the page out when no pointer is on it and the cursor stopped moving, so only
		/// the video is left; a pointer coming back (or a moving one) brings everything back.
		/// </summary>
		private void UpdateChromeVisibility(bool playing) {
			if (_chrome.Count == 0)
				return;

			// the loading indicator and a timeline being dragged own the page
			bool busy    = (loading && loading.activeSelf) || _isUserSeeking;
			bool visible = busy
				|| !playing
				|| _buttonHovered
				|| (_hovered && Time.unscaledTime - _lastActivityAt < hoverHideDelay);
			var target = visible ? 1f : 0f;

			for (var i = 0; i < _chrome.Count; i++) {
				var group = _chrome[i];
				if (!group)
					continue;

				if (!Mathf.Approximately(group.alpha, target))
					group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / ChromeFade);

				bool clickable = group.alpha > 0.5f;
				group.blocksRaycasts = clickable;
				group.interactable   = clickable;
			}
		}
	}
}
