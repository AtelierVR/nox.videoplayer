using System;
using Cysharp.Threading.Tasks;
using Nox.VideoPlayer;

namespace api.nox.videoplayer {
	public class Subtitle : ISubtitle {
		public string Url;
		public string Language;
		public string Title;

		public string GetUrl()
			=> Url;

		public string GetLanguage()
			=> Language;

		public string GetTitle()
			=> Title;

		public UniTask<ISubtitleContent[]> Fetch()
			=> UniTask.FromResult(Array.Empty<ISubtitleContent>());
	}
}