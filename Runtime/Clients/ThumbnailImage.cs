using UnityEngine;
using UnityEngine.UI;

namespace Nox.VideoPlayer.Runtime.Clients {
	/// <summary>
	/// Applies a downloaded texture to an <see cref="Image"/> and releases the sprite and
	/// the texture with the card: the search page rebuilds its cards on every search, so
	/// both must not outlive them.
	/// </summary>
	[RequireComponent(typeof(Image))]
	public class ThumbnailImage : MonoBehaviour {
		private Texture2D _texture;
		private Sprite    _sprite;

		public void Apply(Texture2D texture, AspectRatioFitter ratio) {
			Release();

			var image = GetComponent<Image>();
			if (!image || !texture)
				return;

			_texture      = texture;
			_sprite       = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
			image.sprite  = _sprite;
			image.enabled = true;

			if (ratio && texture.height > 0)
				ratio.aspectRatio = (float)texture.width / texture.height;
		}

		private void Release() {
			if (_sprite) {
				Destroy(_sprite);
				_sprite = null;
			}

			if (_texture) {
				Destroy(_texture);
				_texture = null;
			}
		}

		private void OnDestroy()
			=> Release();
	}
}
