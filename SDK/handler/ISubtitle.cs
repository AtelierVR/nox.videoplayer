using Cysharp.Threading.Tasks;

namespace Nox.VideoPlayer {
	public interface ISubtitle {
		public string Url { get; }

		public string Language { get; }

		public string Title { get; }

		public UniTask<ISubtitleContent[]> Fetch();
	}
}