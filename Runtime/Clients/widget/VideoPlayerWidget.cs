using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.CCK.Utils;
using Nox.UI;
using Nox.UI.Widgets;
using Nox.VideoPlayer.Runtime.Helpers;
using UnityEngine;
using UnityEngine.UI;

namespace Nox.VideoPlayer.Runtime.Clients {
	public class VideoPlayerWidget : MonoBehaviour, IWidget {
		public static string GetDefaultKey()
			=> "videoPlayer";

		public string GetKey()
			=> GetDefaultKey();

		private int _mid;
		private GameObject _content;
		private Image _image;
		private AspectRatioFitter _ratio;
		private GameObject _container;

		private void OnClick()
			=> Client.UiAPI?.SendGoto(_mid, VideoPlayerPage.GetStaticKey());

		public Vector2Int GetSize()
			=> new(2, 2);

		public int GetPriority()
			=> 90;

		public static bool TryMake(IMenu menu, RectTransform parent, out (GameObject, IWidget) values) {
			var prefab    = Client.GetAsset<GameObject>("ui:prefabs/grid_item.prefab");
			var instance  = Instantiate(prefab, parent);
			var component = instance.AddComponent<VideoPlayerWidget>();
			component._mid = menu.Id;

			var button = Reference.GetComponent<Button>("button", instance);
			button.onClick.AddListener(component.OnClick);
			instance.name = $"[{component.GetKey()}_{instance.GetEntityId().GetHashCode()}]";
			values        = (instance, component);

			prefab             = Client.GetAsset<GameObject>("ui:prefabs/widget_image.prefab");
			component._content = Instantiate(prefab, Reference.GetComponent<RectTransform>("content", instance));


			var image = Reference.GetComponent<Image>("image", component._content);
			component._image     = image;
			component._ratio     = Reference.GetComponent<AspectRatioFitter>("ratio", component._content);
			component._container = Reference.GetReference("image_container", component._content);

			component.UpdateIcon().Forget();

			return true;
		}

		public void Update() {
			var videoplayer = VideoPlayerManager.ActiveVideoPlayers.FirstOrDefault();
			if (videoplayer == null) {
				_container.SetActive(false);
				return;
			}

			var texture = videoplayer is IVideoPlayerTexture vpt
				? vpt.Texture
				: null;

			if (!texture || texture.height == 0) {
				_container.SetActive(false);
				return;
			}

			if (!_image.sprite || _image.sprite.texture != texture) {
				_image.sprite = Sprite.Create(
					texture,
					new Rect(0, 0, texture.width, texture.height),
					new Vector2(0.5f, 0.5f)
				);
				var aspect = (float)texture.width / texture.height;
				if (!Mathf.Approximately(_ratio.aspectRatio, aspect))
					_ratio.aspectRatio = aspect;
			}


			_container.SetActive(true);
		}

		private async UniTask UpdateIcon() {
			var icon      = await Client.GetAssetAsync<Sprite>("ui:icons/play_arrow.png");
			var labelIcon = Reference.GetComponent<Image>("icon", _content);
			labelIcon.sprite = icon;
		}
	}
}