using Cysharp.Threading.Tasks;
using Nox.VideoPlayer;
using UnityEngine;

namespace Nox.VideoPlayer.Runtime.Base {
	public class Thumbnail : IThumbnail {
		public string     Url        { get; set; }
		public string     Language   { get; set; }
		public Vector2Int Resolution { get; set; }

		public UniTask<Texture2D> Fetch()
			=> UniTask.FromResult<Texture2D>(null);
	}
}