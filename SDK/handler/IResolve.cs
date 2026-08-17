namespace Nox.VideoPlayer {
	public interface IResolve {
		public string Id { get; }

		public string Title { get; }

		public string Subtitle { get; }

		public string Description { get; }

		public IThumbnail[] Thumbnails { get; }

		public IFormat[] Format { get; }

		public ISubtitle[] Subtitles { get; }

		public (IFormat, IFormat) FindQuality(float quality = -1);
	}
}