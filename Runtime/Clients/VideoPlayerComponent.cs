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

		private float _targetSeekValue;

		// Variables pour gérer le seek par l'utilisateur
		private bool _isUserSeeking;
		private bool _wasPlayingBeforeSeek;

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

			return (content, component);
		}

		public void UpdateProgress(IVideoPlayer player, double progress) {
			if (player == null)
				return;
			if (!_isUserSeeking)
				seek.SetValueWithoutNotify((float)progress);
			loaded.value = 0;
			FormatTime(current, player.Time);
			FormatTime(total, player.Duration);
		}

		public void UpdatePlayStatus(IVideoPlayer player, bool isPlaying) {
			if (player == null)
				return;
			var iconName = isPlaying ? "ui:icons/pause.png" : "ui:icons/play_arrow.png";
			var icon     = Client.GetAsset<Sprite>(iconName);
			if (icon)
				centerIcon.sprite = icon;
		}

		private static void FormatTime(TextLanguage text, double time) {
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

		public void UpdateRender(IVideoPlayer player) {
			var render = player is IVideoPlayerTexture tex ? tex.Texture : null;
			if (!render)
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
			UpdateRender(player);
			UpdatePlayStatus(player, player.IsPlaying);
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
			if (_isUserSeeking)
				return;
			var player = _page.GetSelectedPlayer();
			if (player == null)
				return;
			var duration = player.Duration;
			if (double.IsNaN(duration) || duration <= 0)
				return;
			player.Time           = duration * value;
			_wasPlayingBeforeSeek = player.IsPlaying;
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
			if (player == null)
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
			if (player == null)
				return;
			var duration = player.Duration;
			if (!double.IsNaN(duration) && duration > 0)
				player.Time = seek.value * duration;
			UpdateProgress(player, seek.value);
		}
	}
}