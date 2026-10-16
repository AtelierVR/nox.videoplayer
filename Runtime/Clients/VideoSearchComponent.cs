using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nox.CCK.Language;
using Nox.CCK.Utils;
using Nox.CCK.VideoPlayer;
using Nox.VideoPlayer.Runtime.Processors;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CCKResolver = Nox.CCK.VideoPlayer.VideoPlayerResolver;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.VideoPlayer.Runtime.Clients {
	/// <summary>
	/// Content of the <see cref="VideoSearchPage"/>: a search bar with the results on the
	/// left (full container) and, on the right, the details of the selected result (a
	/// profile card + a Play button) stacked above the container of the video currently
	/// played by the player.
	/// </summary>
	public class VideoSearchComponent : MonoBehaviour {
		private VideoSearchPage _page;

		private TMP_InputField _input;
		private Button         _submit;
		private RectTransform  _results;
		private VideoGridFitter _fitter;
		private GameObject     _scrollGo;
		private TextLanguage   _status;
		private GameObject     _statusGo;
		private GameObject     _cardAsset;
		private GameObject     _widgetAsset;

		// selected result: the profile card (thumbnail + title/subtitle) and the format details
		private TextLanguage      _profileTitle;
		private TextLanguage      _profileIdentifier;
		private Image             _profileBanner;
		private AspectRatioFitter _profileBannerAspect;
		private VideoRatioFitter  _profileBannerRatio;
		private GameObject        _profileWithBanner;
		private GameObject        _profileWithoutBanner;
		private TextLanguage      _detailFormat;
		private TextLanguage      _detailDescription;
		private Button            _play;
		private CancellationTokenSource _thumbnailCts;

		// the video currently played by the player (a container of its own, below the details)
		private RawImage          _currentPreview;
		private AspectRatioFitter _currentPreviewAspect;
		private VideoRatioFitter  _currentRatio;
		private IVideoPlayer      _boundPlayer;
		private Texture           _appliedPreview;

		private IResolve _selected;
		private CancellationTokenSource _searchCts;

		/// <summary>How many results are requested per search.</summary>
		private const uint SearchLimit = 20;

		/// <summary>Height of the Play button of the details container.</summary>
		private const float PlayHeight = 48f;

		/// <summary>Margin around the Play button.</summary>
		private const float PlayMargin = 12f;

		/// <summary>Space reserved at the bottom of the details for the Play button.</summary>
		private const float PlayReserved = PlayHeight + 2f * PlayMargin;

		#region Generate

		public static (GameObject, VideoSearchComponent) Generate(VideoSearchPage page, RectTransform parent) {
			var splitAsset       = Client.GetAsset<GameObject>("ui:prefabs/split.prefab");
			var containerAsset   = Client.GetAsset<GameObject>("ui:prefabs/container.prefab");
			var containerFull    = Client.GetAsset<GameObject>("ui:prefabs/container_full.prefab");
			var withSearchAsset  = Client.GetAsset<GameObject>("ui:prefabs/with_search.prefab");
			var scrollAsset      = Client.GetAsset<GameObject>("ui:prefabs/scroll.prefab");
			var textAsset        = Client.GetAsset<GameObject>("ui:prefabs/text.prefab");
			var btnAsset         = Client.GetAsset<GameObject>("ui:prefabs/btn_icon.prefab");

			var content = splitAsset.Instantiate(parent);
			var component = content.AddComponent<VideoSearchComponent>();
			component._page = page;
			content.name = $"[{page.GetKey()}_{content.GetId()}]";
			var splitContent = Reference.GetComponent<RectTransform>("content", content);

			// ── Left: search bar + results (full container) ───────────────
			var left      = containerFull.Instantiate(splitContent);
			var withSearch = withSearchAsset.Instantiate(Reference.GetComponent<RectTransform>("content", left));
			var header     = Reference.GetReference("header", withSearch);

			component._input  = Reference.GetComponent<TMP_InputField>("input", header);
			component._submit = Reference.GetComponent<Button>("submit", header);
			if (component._input) {
				var placeholder = component._input.placeholder
					? component._input.placeholder.GetComponent<TextLanguage>()
					: null;
				placeholder?.UpdateText("video_player.search.placeholder");
				component._input.text = PlayerUrl(page);
			}

			var searchContent = Reference.GetComponent<RectTransform>("content", withSearch);

			// results: a scrollable grid of cards (like the search results for worlds/mods/avatars)
			var scroll = scrollAsset.Instantiate(searchContent);
			component._scrollGo = scroll;

			var grid = new GameObject("grid", typeof(RectTransform));
			grid.transform.SetParent(Reference.GetComponent<RectTransform>("content", scroll), false);
			component._results = (RectTransform)grid.transform;
			grid.AddComponent<GridLayoutGroup>();
			component._fitter = grid.AddComponent<VideoGridFitter>();
			var sizeFitter = grid.AddComponent<ContentSizeFitter>();
			sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			// status text: sibling of the scroll, shown when there is no result
			var status = textAsset.Instantiate(searchContent);
			component._statusGo = status;
			component._status   = Reference.GetComponent<TextLanguage>("text", status);
			component._status?.UpdateText("video_player.search.start");

			// ── Right: details of the selected result, then the current video ─────
			// The two are separate containers: the preview of the player is not part of the
			// details container, it is the sibling below it.
			var columnGo = new GameObject("details", typeof(RectTransform));
			var column   = (RectTransform)columnGo.transform;
			column.SetParent(splitContent, false);

			// same width as a bare container: the search panel takes the remaining space
			var columnWidth = columnGo.GetOrAddComponent<LayoutElement>();
			columnWidth.minWidth       = 420;
			columnWidth.preferredWidth = 420;
			columnWidth.flexibleWidth  = 0;

			var columnLayout = columnGo.AddComponent<VerticalLayoutGroup>();
			columnLayout.spacing                = 32;
			columnLayout.childControlWidth      = true;
			columnLayout.childControlHeight     = true;
			columnLayout.childForceExpandWidth  = true;
			columnLayout.childForceExpandHeight = false;

			// B: the details of the selected result. No header: the profile card carries
			// everything, so `with_title` is not used here.
			var details = containerAsset.Instantiate(column);
			details.name = "details_container";
			details.GetOrAddComponent<LayoutElement>().flexibleHeight = 1000;

			var panel   = Reference.GetComponent<RectTransform>("content", details);
			var profile = component.CreateProfileCard(panel);

			// the profile prefab reserves a slot under its title: the format, the description
			// and the Play button go there.
			var profileContent = Reference.GetComponent<RectTransform>("content", profile);
			var profileLayout  = profileContent.gameObject.AddComponent<VerticalLayoutGroup>();
			profileLayout.padding                = new RectOffset(12, 12, 12, 12);
			profileLayout.spacing                = 8;
			profileLayout.childControlWidth      = true;
			profileLayout.childControlHeight     = true;
			profileLayout.childForceExpandWidth  = true;
			profileLayout.childForceExpandHeight = false;

			component._detailFormat      = MakeText(textAsset, profileContent, "video_player.search.detail.format");
			component._detailDescription = MakeText(textAsset, profileContent, "video_player.search.detail.description");

			// spacer: pushes the play button to the bottom of the container
			var spacer = new GameObject("spacer", typeof(RectTransform));
			spacer.transform.SetParent(profileContent, false);
			spacer.AddComponent<LayoutElement>().flexibleHeight = 1000;

			// The play button is anchored to the bottom of the details container, outside of the
			// layout chain, and the profile is inset by its height: a long title can no longer
			// push the button out of the container when the panel is too small.
			var inset = panel.offsetMin;
			panel.offsetMin = new Vector2(inset.x, inset.y + PlayReserved);

			var play     = btnAsset.Instantiate((RectTransform)details.transform);
			var playRect = (RectTransform)play.transform;
			playRect.anchorMin = new Vector2(0f, 0f);
			playRect.anchorMax = new Vector2(1f, 0f);
			playRect.pivot     = new Vector2(0.5f, 0f);
			playRect.offsetMin = new Vector2(PlayMargin, PlayMargin);
			playRect.offsetMax = new Vector2(-PlayMargin, PlayMargin + PlayHeight);
			play.GetOrAddComponent<LayoutElement>().ignoreLayout = true;
			component._play     = Reference.GetComponent<Button>("button", play);
			Reference.GetComponent<TextLanguage>("text", play)?.UpdateText("video_player.search.play");
			var playIcon = Reference.GetComponent<Image>("image", play);
			if (playIcon) playIcon.sprite = Client.GetAsset<Sprite>("ui:icons/play_arrow.png");
			if (component._play) component._play.onClick.AddListener(component.OnPlay);

			// C: the video currently played, in its own container under the details
			component.CreateCurrentVideo(column);

			component.UpdateDetail(null);
			return (content, component);
		}

		private static TextLanguage MakeText(GameObject textAsset, RectTransform parent, string key) {
			var go   = textAsset.Instantiate(parent);
			var text = Reference.GetComponent<TextLanguage>("text", go);
			text?.UpdateText(key);
			return text;
		}

		private static string PlayerUrl(VideoSearchPage page)
			=> page.GetPlayer() is IVideoPlayerSource source ? source.Url ?? string.Empty : string.Empty;

		#endregion

		#region Unity lifecycle

		private void Start() {
			if (_input) _input.onSubmit.AddListener(OnSubmit);
			if (_submit) _submit.onClick.AddListener(OnSubmit);
			if (_page != null && !_page.CanControl)
				Logger.LogWarning("The user is not allowed to change the video on this player (read-only)", this, nameof(VideoSearchComponent));
			UpdatePlay();
			BindCurrent();
		}

		/// <summary>
		/// <c>IVideoPlayerVideo.OnTexture</c> is only raised when the decoder swaps its frame
		/// buffer, and the read thread may start before the player announces its new state:
		/// the preview therefore also re-checks the current texture every frame (a reference
		/// comparison only, the texture is updated in place otherwise).
		/// </summary>
		private void Update() {
			if (!(_boundPlayer is IVideoPlayerVideo video))
				return;

			var texture = video.Texture;
			if (texture == _appliedPreview)
				return;

			_appliedPreview = texture;
			ApplyPreview(texture);
		}

		private void OnDestroy() {
			_searchCts?.Cancel();
			_searchCts = null;
			_thumbnailCts?.Cancel();
			_thumbnailCts = null;
			UnbindCurrent();
			if (_input) _input.onSubmit.RemoveListener(OnSubmit);
			if (_submit) _submit.onClick.RemoveListener(OnSubmit);
			if (_play) _play.onClick.RemoveListener(OnPlay);
		}

		#endregion

		#region Search

		private void OnSubmit()
			=> OnSubmit(_input ? _input.text : string.Empty);

		private void OnSubmit(string query) {
			if (_page == null)
				return;
			if (!_page.CanControl) {
				SetStatus("video_player.search.forbidden");
				return;
			}
			if (string.IsNullOrWhiteSpace(query)) {
				SetStatus("video_player.search.start");
				return;
			}
			SearchAsync(query.Trim()).Forget();
		}

		private async UniTaskVoid SearchAsync(string query) {
			var player = _page.GetPlayer();
			if (player == null) {
				SetStatus("video_player.search.no_player");
				return;
			}

			_searchCts?.Cancel();
			_searchCts = new CancellationTokenSource();

			ClearResults();
			SetStatus("video_player.search.searching");

			var options = new VideoFetchOptions {
				Query        = NormalizeQuery(query),
				Limit        = SearchLimit,
				Page         = 0,
				Cancellation = _searchCts,
			};

			try {
				var results  = await CCKResolver.Resolve(player, options);
				var resolves = (results ?? Array.Empty<IResult>())
					.Where(r => r != null && !r.IsError)
					.SelectMany(r => r.Data ?? Array.Empty<IResolve>())
					.Where(r => r != null)
					.ToArray();

				RenderResults(resolves);
			} catch (Exception e) {
				Logger.LogError(e);
				SetStatus("video_player.search.error");
			}
		}

		/// <summary>
		/// A full url or an explicit handler prefix is passed through; a free text is
		/// searched through the default (YouTube) handler, the only one able to turn a
		/// text into results.
		/// </summary>
		private static string NormalizeQuery(string query)
			=> query.Contains("://")
				|| query.StartsWith("youtube:", StringComparison.OrdinalIgnoreCase)
				|| query.StartsWith("twitch:", StringComparison.OrdinalIgnoreCase)
				|| query.StartsWith("search:", StringComparison.OrdinalIgnoreCase)
					? query
					: "youtube:" + query;

		private void RenderResults(IResolve[] resolves) {
			ClearResults();

			if (resolves.Length == 0) {
				SetStatus("video_player.search.no_result");
				return;
			}

			SetStatus(null); // show the grid

			_cardAsset   ??= Client.GetAsset<GameObject>("ui:prefabs/grid_item.prefab");
			_widgetAsset ??= Client.GetAsset<GameObject>("ui:prefabs/large_widget.prefab");

			foreach (var resolve in resolves) {
				var card = _cardAsset.Instantiate(_results);
				var captured = resolve;
				Reference.GetComponent<Button>("button", card)?.onClick.AddListener(() => Select(captured));

				var widget = _widgetAsset.Instantiate(Reference.GetComponent<RectTransform>("content", card));
				Reference.GetComponent<TextLanguage>("label", widget)
					?.UpdateText("video_player.search.result", new[] { resolve.Title ?? "" });
				SetThumbnail(
					Reference.GetComponent<Image>("image", widget),
					Reference.GetReference("image_container", widget),
					Reference.GetComponent<AspectRatioFitter>("image_ratio", widget),
					BestThumbnail(resolve)
				);

				card.SetActive(true);
			}

			RefreshGridLayout();
		}

		/// <summary>Fits the grid cells to the available width, then rebuilds the layout.</summary>
		private void RefreshGridLayout() {
			// Force a pass so the grid has a real width before deriving the cell size.
			if (_scrollGo)
				UpdateLayout.UpdateImmediate(_scrollGo);

			_fitter?.UpdateLayout();

			if (_scrollGo)
				UpdateLayout.UpdateImmediate(_scrollGo);
			else if (_results)
				UpdateLayout.UpdateImmediate(_results);
		}

		private void ClearResults() {
			_selected = null;
			if (_results)
				foreach (Transform child in _results)
					child.gameObject.Destroy();
			UpdateDetail(null);
		}

		/// <summary>Shows either the results grid (<paramref name="key"/> null) or a status message.</summary>
		private void SetStatus(string key) {
			var showStatus = !string.IsNullOrEmpty(key);
			if (_statusGo) _statusGo.SetActive(showStatus);
			if (_scrollGo) _scrollGo.SetActive(!showStatus);
			if (!showStatus)
				return;
			_status?.UpdateText(key);
		}

		/// <summary>
		/// Applies the best thumbnail of a result to a card. The image is downloaded through
		/// <see cref="DownloadHandlerImage"/> (which also decodes the webp payloads Unity
		/// cannot read) and the container is hidden when there is none.
		/// </summary>
		private void SetThumbnail(Image image, GameObject container, AspectRatioFitter ratio, string url) {
			if (!image)
				return;
			if (string.IsNullOrEmpty(url)) {
				image.sprite = null;
				container?.SetActive(false);
				return;
			}

			container?.SetActive(true);
			LoadThumbnail(image, container, ratio, url).Forget();
		}

		private async UniTaskVoid LoadThumbnail(Image image, GameObject container, AspectRatioFitter ratio, string url) {
			var texture = await DownloadHandlerImage.Fetch(url, _searchCts?.Token ?? default);
			// the card may have been rebuilt (new search) while the image was downloading
			if (!texture || !image || !this)
				return;

			image.GetOrAddComponent<ThumbnailImage>().Apply(texture, ratio);
			container?.SetActive(true);
		}

		/// <summary>Url of the largest thumbnail offered by a result, or null.</summary>
		private static string BestThumbnail(IResolve resolve) {
			var thumbnails = resolve?.Thumbnails;
			if (thumbnails == null || thumbnails.Length == 0)
				return null;
			return thumbnails
				.Where(t => t != null && !string.IsNullOrEmpty(t.Url))
				.OrderByDescending(t => (long)t.Resolution.x * t.Resolution.y)
				.Select(t => t.Url)
				.FirstOrDefault();
		}

		#endregion

		#region Details

		private void Select(IResolve resolve) {
			_selected = resolve;
			UpdateDetail(resolve);
		}

		private void UpdateDetail(IResolve resolve) {
			if (resolve == null) {
				_profileTitle?.UpdateText("video_player.search.detail.title", new[] { "-" });
				_profileIdentifier?.UpdateText("video_player.search.detail.subtitle", new[] { "-" });
				_detailFormat?.UpdateText("video_player.search.detail.format", new[] { "-" });
				_detailDescription?.UpdateText("video_player.search.detail.description", new[] { "-" });
			} else {
				_profileTitle?.UpdateText("video_player.search.detail.title", new[] { resolve.Title ?? "" });
				_profileIdentifier?.UpdateText("video_player.search.detail.subtitle", new[] { resolve.Subtitle ?? "" });
				_detailFormat?.UpdateText("video_player.search.detail.format", new[] { FormatFormats(resolve) });
				_detailDescription?.UpdateText("video_player.search.detail.description", new[] { resolve.Description ?? "" });
			}
			UpdatePlay();
			SetProfileThumbnail(resolve == null ? null : BestThumbnail(resolve));
		}

		/// <summary>
		/// Switches the banner of the profile card between the media poster and the placeholder
		/// of the prefab: the placeholder is used as long as there is nothing to display (no
		/// selection, no thumbnail, download in progress or failed).
		/// </summary>
		private void ShowProfileBanner(bool withBanner) {
			_profileWithBanner?.SetActive(withBanner);
			_profileWithoutBanner?.SetActive(!withBanner);
		}

		/// <summary>
		/// Posters the selected result on the profile card of the details container: the previous
		/// thumbnail is dropped, the placeholder is shown, then the new thumbnail is downloaded
		/// (webp included) before the banner replaces the placeholder, so the card never displays
		/// a stale poster.
		/// </summary>
		private void SetProfileThumbnail(string url) {
			_thumbnailCts?.Cancel();
			_thumbnailCts?.Dispose();
			_thumbnailCts = null;

			ShowProfileBanner(false);

			if (string.IsNullOrEmpty(url) || !_profileBanner)
				return;

			_thumbnailCts = new CancellationTokenSource();
			LoadProfileThumbnail(url, _thumbnailCts.Token).Forget();
		}

		private async UniTaskVoid LoadProfileThumbnail(string url, CancellationToken token) {
			var texture = await DownloadHandlerImage.Fetch(url, token);
			if (token.IsCancellationRequested || !this || !_profileBanner)
				return;

			// a failed download keeps the placeholder of the prefab
			if (!texture)
				return;

			_profileBanner.GetOrAddComponent<ThumbnailImage>().Apply(texture, _profileBannerAspect);
			if (_profileBannerRatio && texture.height > 0)
				_profileBannerRatio.AspectRatio = (float)texture.width / texture.height;

			ShowProfileBanner(true);
		}

		private void UpdatePlay() {
			if (!_play)
				return;
			var allowed = _page != null && _page.CanControl;
			_play.interactable = allowed && _selected != null;
		}

		private void OnPlay() {
			if (_page == null || _selected == null)
				return;
			if (!_page.CanControl) {
				SetStatus("video_player.search.forbidden");
				return;
			}
			if (_page.GetPlayer() is IVideoPlayerSource source)
				source.Play(_selected);
		}

		private static string FormatFormats(IResolve resolve) {
			if (resolve?.Format == null || resolve.Format.Length == 0)
				return "-";
			var (video, audio) = resolve.FindQuality();
			var parts = new List<string>();
			if (video is IVideo v) {
				if (v.Resolution.x > 0 && v.Resolution.y > 0)
					parts.Add($"{v.Resolution.x}x{v.Resolution.y}");
				if (v.Framerate > 0)
					parts.Add($"{v.Framerate}fps");
				if (!string.IsNullOrEmpty(v.VideoCodec))
					parts.Add(v.VideoCodec);
			}
			if (video != null)
				parts.Add($"q{video.Quality:0.##}");
			if (audio is IAudio a && !string.IsNullOrEmpty(a.AudioCodec))
				parts.Add(a.AudioCodec);
			return parts.Count > 0 ? string.Join(" · ", parts) : resolve.Format.Length + " format(s)";
		}

		/// <summary>
		/// Adds the card of the selected result to the details container: a <c>profile</c> prefab
		/// showing the thumbnail (through its banner, so the poster keeps its aspect ratio)
		/// along with the title and the subtitle of the media.
		/// </summary>
		/// <returns>The profile instance, whose <c>content</c> slot is left for the caller.</returns>
		private GameObject CreateProfileCard(RectTransform parent) {
			var profile = Client.GetAsset<GameObject>("ui:prefabs/profile.prefab").Instantiate(parent);

			_profileTitle        = Reference.GetComponent<TextLanguage>("title", profile);
			_profileIdentifier   = Reference.GetComponent<TextLanguage>("identifier", profile);
			_profileBanner       = Reference.GetComponent<Image>("banner", profile);
			_profileBannerAspect = Reference.GetComponent<AspectRatioFitter>("banner_aspect", profile);
			_profileWithBanner   = Reference.GetReference("with_banner", profile);
			_profileWithoutBanner = Reference.GetReference("without_banner", profile);

			// The banner is only shown once a thumbnail has been downloaded
			ShowProfileBanner(false);

			// The container of the banner (and its mask) must hug the poster: its
			// ContentSizeFitter cannot do it (the mask sprite is square and the layout group
			// adds a padding), so its height is driven by the ratio of the image instead.
			if (_profileWithBanner) {
				var sizeFitter = _profileWithBanner.GetComponent<ContentSizeFitter>();
				if (sizeFitter) sizeFitter.enabled = false;

				var group = _profileWithBanner.GetComponent<VerticalLayoutGroup>();
				if (group) group.padding = new RectOffset();

				_profileWithBanner.GetOrAddComponent<LayoutElement>().flexibleHeight = 0;
				_profileBannerRatio = _profileWithBanner.AddComponent<VideoRatioFitter>();
				_profileBannerRatio.source = (RectTransform)_profileWithBanner.transform;
				// the poster of the selected media always keeps its ratio: the player below is the
				// one that gives way when the panel is too small.
				_profileBannerRatio.shrinkable = false;
			}

			// the prefab also carries a small avatar thumbnail and a row of buttons: neither is
			// used here and both would only eat space in the container.
			Reference.GetReference("with_thumbnail", profile)?.SetActive(false);
			Reference.GetReference("without_thumbnail", profile)?.SetActive(false);

			// The banner is normally driven by a NetworkImage (a type declared by another
			// assembly): it clears the texture when it is disabled, which would erase the
			// thumbnail applied here.
			var networkImage = _profileBanner ? _profileBanner.GetComponent("NetworkImage") : null;
			if (networkImage) networkImage.Destroy();

			return profile;
		}

		#endregion

		#region Current video

		/// <summary>
		/// Adds, below the details container, the container of the video currently played by the
		/// player. It is not part of the details: it takes the whole width of the column and its
		/// height follows the ratio of the video (see <see cref="VideoRatioFitter"/>).
		/// </summary>
		private void CreateCurrentVideo(RectTransform parent) {
			var preview = Client.GetAsset<GameObject>("ui:prefabs/container_full.prefab").Instantiate(parent);
			preview.name = "current_video";

			var previewLayout = preview.GetOrAddComponent<LayoutElement>();
			previewLayout.flexibleWidth  = 1;
			previewLayout.flexibleHeight = 0;

			// the width comes from the column, the height is derived from it and the ratio; this
			// container is the one that gives way when the column is too short (see shrinkable)
			_currentRatio = preview.AddComponent<VideoRatioFitter>();
			_currentRatio.source     = (RectTransform)preview.transform;
			_currentRatio.shrinkable = true;

			_currentPreview       = MakeRawImage(Reference.GetComponent<RectTransform>("content", preview));
			_currentPreviewAspect = _currentPreview.GetComponent<AspectRatioFitter>();
		}

		/// <summary>Creates a raw image filling its parent, with an aspect fitter.</summary>
		private static RawImage MakeRawImage(RectTransform parent) {
			var go = new GameObject("image", typeof(RectTransform), typeof(RawImage));
			var rt = (RectTransform)go.transform;
			rt.SetParent(parent, false);
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;

			var raw = go.GetComponent<RawImage>();
			raw.raycastTarget = false;
			raw.enabled       = false;

			var ratio = go.AddComponent<AspectRatioFitter>();
			ratio.aspectMode  = AspectRatioFitter.AspectMode.FitInParent;
			ratio.aspectRatio = 16f / 9f;
			return raw;
		}

		/// <summary>Shows a texture in a raw image, keeping its aspect ratio.</summary>
		private static void ApplyTexture(RawImage image, AspectRatioFitter ratio, Texture texture) {
			if (!image)
				return;

			image.texture = texture;
			image.enabled = texture != null;

			if (ratio && texture && texture.height > 0)
				ratio.aspectRatio = (float)texture.width / texture.height;
		}

		/// <summary>
		/// Shows a frame in the preview of the current video and re-sizes its container to the
		/// ratio of the media.
		/// </summary>
		private void ApplyPreview(Texture texture) {
			ApplyTexture(_currentPreview, _currentPreviewAspect, texture);

			if (_currentRatio && texture && texture.height > 0)
				_currentRatio.AspectRatio = (float)texture.width / texture.height;
		}

		/// <summary>Binds the preview to the player of the page, if it is not already bound.</summary>
		private void BindCurrent() {
			var player = _page?.GetPlayer();
			if (ReferenceEquals(player, _boundPlayer))
				return;

			UnbindCurrent();
			_boundPlayer    = player;
			_appliedPreview = null;

			if (_boundPlayer is IVideoPlayerVideo video) {
				video.OnTexture.AddListener(OnCurrentTexture);
				ApplyPreview(video.Texture);
				_appliedPreview = video.Texture;
			} else {
				ApplyPreview(null);
			}
		}

		private void UnbindCurrent() {
			if (_boundPlayer is IVideoPlayerVideo video)
				video.OnTexture.RemoveListener(OnCurrentTexture);

			_boundPlayer = null;
		}

		private void OnCurrentTexture(IVideoPlayer player, Texture2D texture) {
			if (texture == null || texture == _appliedPreview)
				return;

			_appliedPreview = texture;
			ApplyPreview(texture);
		}

		#endregion
	}
}
