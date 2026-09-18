using Nox.CCK.Language;
using Nox.CCK.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

		private float _targetSeekValue;

		// Variables pour gérer le seek par l'utilisateur
		private bool _isUserSeeking;
		private bool _wasPlayingBeforeSeek;
		private bool _live;

		/// Player whose bottom bar has already been resolved (see <see cref="UpdateStream"/>).
		private IVideoPlayer _streamPlayer;

		public static (GameObject, VideoPlayerComponent) Generate(VideoPlayerPage page, RectTransform parent) {
			var content = Instantiate(Client.GetAsset<GameObject>("ui:prefabs/split.prefab"), parent);

			var component = content.AddComponent<VideoPlayerComponent>();
			component._page = page;
			content.name    = $"[{page.GetKey()}_{content.GetEntityId().GetHashCode()}]";
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

			// bottom bar: the media (seek) and live variants are exclusive
			component.mediaContainer = Reference.GetReference("video_player.media_container", videoPlayer);
			component.liveContainer  = Reference.GetReference("video_player.live_container", videoPlayer);
			component.liveCurrent    = FindLiveTime(component.liveContainer);

			return (content, component);
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
			var player = _page.GetSelectedPlayer();
			if (player == null)
				return;
			if (player != _streamPlayer)
				UpdateStream(player); // another player is displayed: its bar may differ
			UpdateRender(player);
			UpdatePlayStatus(player, player.State);
			UpdateProgress(player, player.Progress);
			UpdateTitle(player);
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
			var player = _page.GetSelectedPlayer();
			if (player == null)
				return;
			var duration = player.Duration;
			if (double.IsNaN(duration) || duration <= 0)
				return;
			player.Time           = duration * value;
			_wasPlayingBeforeSeek = player.State == State.Playing;
			if (_wasPlayingBeforeSeek) {
				player.Pause();
			}
		}

		public void OnSeekStart() {
			_isUserSeeking = true;
		}

		public void OnSeekEnd() {
			_isUserSeeking = false;
			var player = _page.GetSelectedPlayer();
			if (player == null || _live || !seek)
				return;
			var duration = player.Duration;
			if (!double.IsNaN(duration) && duration > 0)
				player.Time = seek.value * duration;
			if (_wasPlayingBeforeSeek)
				player.Resume();
			UpdateProgress(player, seek.value);
		}

		public void OnSeekDrag() {
			var player = _page.GetSelectedPlayer();
			if (player == null || _live || !seek)
				return;
			var duration = player.Duration;
			if (!double.IsNaN(duration) && duration > 0)
				player.Time = seek.value * duration;
			UpdateProgress(player, seek.value);
		}
	}
}