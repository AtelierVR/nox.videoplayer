using System;

namespace Nox.VideoPlayer {
	public interface IFormat {
		public string GetUrl();

		public string GetContainer();

		public string GetLanguage();

		public uint GetBitrate();

		public float GetQuality();
	}
}