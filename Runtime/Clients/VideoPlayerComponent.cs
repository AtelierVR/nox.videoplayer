using Nox.CCK.Language;
using Nox.CCK.UI;
using Nox.CCK.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.VideoPlayer.Runtime.Clients {
	public class VideoPlayerComponent : MonoBehaviour {
		private VideoPlayerPage _page;

		public AspectRatioFitter ratio;
		public Image video;
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

		/// <summary>Search button shown at the top-right of the title (null when absent).</summary>
		private GameObject _searchButton;
		/// <summary>Last applied visibility of the search button (avoid a LINQ lookup per frame).</summary>
		private bool _searchAllowed;

		// Seek handling: the timeline must stay stable and immediate while seeking.
		private bool _isUserSeeking;
		private bool _live;

		/// Player whose bottom bar has already been resolved (see <see cref="UpdateStream"/>).
		private IVideoPlayer _streamPlayer;

		/// <summary>CanvasGroup fading the play/pause button in and out (Reference of the prefab).</summary>
		private CanvasGroup _center;
		/// <summary>Number of pointers (mouse, UI ray…) currently on the page.</summary>
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
			component.video          = Reference.GetComponent<Image>("video", videoPlayer);
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
			var actions = Reference.GetReference("actions_contanier", videoPlayer)
				?? Reference.GetReference("actions_contanier");
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
		/// Called when the player opens another stream (Play, track change…): the
		/// duration is only known at that moment, so this is where the bottom bar is
		/// resolved — no need to check it every frame.
		/// </summary>
		public void UpdateStream(IVideoPlayer player) {
			_streamPlayer = player;
			SetLive(player != null && IsStream(player));
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

		public void UpdateRender(IVideoPlayer player) {
			var render = player is IVideoPlayerVideo tex ? tex.Texture : null;
			if (!render || !video || !ratio)
				return;
			video.material.mainTexture = render;
			ratio.aspectRatio          = (float)render.width / render.height;
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
			if (player == null)
				return;
			if (player != _streamPlayer)
				UpdateStream(player); // another player is displayed: its bar may differ
			UpdateRender(player);
			UpdatePlayStatus(player, player.State);
			UpdateProgress(player, player.Progress);
			UpdateTitle(player);
			UpdateLoading(player.State);
			UpdateCenterVisibility(IsPlaying(player.State));
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
					|| (_hovered && Time.unscaledTime - _lastActivityAt < hoverHideDelay));
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
	}
}