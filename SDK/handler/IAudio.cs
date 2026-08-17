namespace Nox.VideoPlayer {
	public interface IAudio : IFormat {
		public uint AudioChannels { get; }

		public uint AudioBitrate { get; }

		public string AudioCodec { get; }
	}
}