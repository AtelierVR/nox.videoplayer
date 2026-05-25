namespace Nox.VideoPlayer {
	public interface IResolve {
		public string GetId();

		public string GetTile();

		public string GetSubtitle();

		public string GetDescription();

		public IThumbnail[] GetThumbnails();

		public IFormat[] GetFormat();

		public ISubtitle[] GetSubtitles();

		public (IFormat, IFormat) FindQuality(float quality = -1);
	}
}