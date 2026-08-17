using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nox.VideoPlayer {
	public interface IThumbnail {
		public string Url { get; }

		public string Language { get; }

		public Vector2Int Resolution { get; }

		public UniTask<Texture2D> Fetch();
	}
}