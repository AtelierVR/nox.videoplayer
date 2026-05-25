namespace Nox.VideoPlayer {
	public interface IAudio : IFormat {
		public uint GetAudioChannels();

		public uint GetAudioBitrate();

		public string GetAudioCodec();
	}
}