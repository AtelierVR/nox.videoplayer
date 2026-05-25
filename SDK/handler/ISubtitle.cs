using Cysharp.Threading.Tasks;

namespace Nox.VideoPlayer {
	public interface ISubtitle {
		public string GetUrl();

		public string GetLanguage();

		public string GetTitle();

		public UniTask<ISubtitleContent[]> Fetch();
	}
}