using Cysharp.Threading.Tasks;
using Nox.VideoPlayer;
using UnityEngine;

namespace Nox.VideoPlayer.Runtime.Base {
	public class Thumbnail : IThumbnail {
		public string     Url;
		public string     Language;
		public Vector2Int Resolution;

		public string GetUrl()
			=> Url;

		public string GetLanguage()
			=> Language;

		public Vector2Int GetResolution()
			=> Resolution;

		public UniTask<Texture2D> Fetch()
			=> UniTask.FromResult<Texture2D>(null);
	}
}