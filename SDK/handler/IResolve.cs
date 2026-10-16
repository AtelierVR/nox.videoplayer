namespace Nox.VideoPlayer {
	public interface IResolve {
		public string Id { get; }

		/// <summary>
		/// The page url of the media, when the source only gives a "flat" entry: a search
		/// result carries its id and this url, but no playable format.
		/// </summary>
		public string Url { get; }

		public string Title { get; }

		public string Subtitle { get; }

		public string Description { get; }

		public IThumbnail[] Thumbnails { get; }

		public IFormat[] Format { get; }

		public ISubtitle[] Subtitles { get; }

		public (IFormat, IFormat) FindQuality(float quality = -1);
	}
}