namespace Nox.VideoPlayer {
	public interface ISubtitleContent {
		public float  Start { get; }
		public float  End { get; }
		public string Text { get; }
	}
}