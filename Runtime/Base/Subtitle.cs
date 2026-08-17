using System;
using Cysharp.Threading.Tasks;
using Nox.VideoPlayer;

namespace Nox.VideoPlayer.Runtime.Base {
	public class Subtitle : ISubtitle {
		public string Url      { get; set; }
		public string Language { get; set; }
		public string Title    { get; set; }

		public UniTask<ISubtitleContent[]> Fetch()
			=> UniTask.FromResult(Array.Empty<ISubtitleContent>());
	}
}