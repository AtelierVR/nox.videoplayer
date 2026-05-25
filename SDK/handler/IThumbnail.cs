using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nox.VideoPlayer {
	public interface IThumbnail {
		public string GetUrl();

		public string GetLanguage();

		public Vector2Int GetResolution();

		public UniTask<Texture2D> Fetch();
	}
}